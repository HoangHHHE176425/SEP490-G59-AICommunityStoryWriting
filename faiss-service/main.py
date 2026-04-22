import json
import logging
import os
from pathlib import Path
from typing import List, Optional
from uuid import UUID

import faiss
import numpy as np
from fastapi import FastAPI, Header, HTTPException
from pydantic import BaseModel, Field


DATA_DIR = Path(os.getenv("FAISS_DATA_DIR", "./data/faiss"))
API_KEY = os.getenv("FAISS_API_KEY", "").strip()
HNSW_M = int(os.getenv("FAISS_HNSW_M", "32"))
HNSW_EF_CONSTRUCTION = int(os.getenv("FAISS_HNSW_EF_CONSTRUCTION", "200"))
HNSW_EF_SEARCH = int(os.getenv("FAISS_HNSW_EF_SEARCH", "64"))
DATA_DIR.mkdir(parents=True, exist_ok=True)

LOG_LEVEL = os.getenv("FAISS_LOG_LEVEL", "INFO").upper()
logging.basicConfig(
    level=getattr(logging, LOG_LEVEL, logging.INFO),
    format="%(asctime)s %(levelname)s [faiss-service] %(message)s",
)
logger = logging.getLogger("faiss-service")

app = FastAPI(title="FAISS Service", version="1.0.0")


class StoryOnlyRequest(BaseModel):
    storyId: UUID


class UpsertRequest(BaseModel):
    storyId: UUID
    chunkIds: List[UUID]
    chapterIds: List[UUID]
    vectors: List[List[float]]
    contents: List[str]
    indexedChapterIds: List[UUID] = Field(default_factory=list)


class ChunkInfosRequest(BaseModel):
    storyId: UUID
    chunkIds: List[UUID] = Field(default_factory=list)


class SearchRequest(BaseModel):
    storyId: UUID
    queryVector: List[float]
    topK: int = 20


class SearchItem(BaseModel):
    chunkId: UUID
    score: float


class SearchResponse(BaseModel):
    results: List[SearchItem]


class ChunkInfoItem(BaseModel):
    chunkId: UUID
    chapterId: UUID
    content: str


class ChunkInfosResponse(BaseModel):
    items: List[ChunkInfoItem]


class FullIndexResponse(BaseModel):
    ids: List[UUID]
    chapterIds: List[UUID]
    vectors: List[List[float]]
    contents: List[str]


def _validate_key(x_api_key: Optional[str]):
    if API_KEY and x_api_key != API_KEY:
        raise HTTPException(status_code=401, detail="Invalid FAISS API key.")


def _index_path(story_id: UUID) -> Path:
    return DATA_DIR / f"{story_id}.index"


def _meta_path(story_id: UUID) -> Path:
    return DATA_DIR / f"{story_id}.meta.json"


def _normalize(vectors: np.ndarray) -> np.ndarray:
    norms = np.linalg.norm(vectors, axis=1, keepdims=True)
    norms[norms == 0] = 1.0
    return vectors / norms


def _build_hnsw_index(dim: int) -> faiss.IndexHNSWFlat:
    m = HNSW_M if HNSW_M > 0 else 32
    ef_construction = HNSW_EF_CONSTRUCTION if HNSW_EF_CONSTRUCTION > 0 else 200
    ef_search = HNSW_EF_SEARCH if HNSW_EF_SEARCH > 0 else 64

    index = faiss.IndexHNSWFlat(dim, m, faiss.METRIC_INNER_PRODUCT)
    index.hnsw.efConstruction = ef_construction
    index.hnsw.efSearch = ef_search
    return index


def _read_meta(story_id: UUID) -> dict:
    mp = _meta_path(story_id)
    if not mp.exists():
        return {"chunkIds": [], "chapterIds": [], "contents": [], "indexedChapterIds": [], "vectors": []}
    with mp.open("r", encoding="utf-8") as f:
        return json.load(f)


def _write_meta(story_id: UUID, meta: dict):
    with _meta_path(story_id).open("w", encoding="utf-8") as f:
        json.dump(meta, f, ensure_ascii=False)


@app.get("/health")
def health():
    return {"ok": True}


@app.post("/v1/index/upsert")
def upsert_index(req: UpsertRequest, x_api_key: Optional[str] = Header(default=None)):
    _validate_key(x_api_key)
    logger.info("upsert called storyId=%s chunks=%d", req.storyId, len(req.chunkIds))

    n = len(req.chunkIds)
    if n == 0:
        logger.info("upsert skipped storyId=%s reason=empty_chunks", req.storyId)
        return {"ok": True, "chunkCount": 0}
    if not (n == len(req.chapterIds) == len(req.vectors) == len(req.contents)):
        raise HTTPException(status_code=400, detail="chunkIds/chapterIds/vectors/contents lengths mismatch.")

    vectors = np.array(req.vectors, dtype="float32")
    if vectors.ndim != 2:
        raise HTTPException(status_code=400, detail="vectors must be a 2D array.")

    vectors = _normalize(vectors)
    dim = vectors.shape[1]
    index = _build_hnsw_index(dim)
    index.add(vectors)
    faiss.write_index(index, str(_index_path(req.storyId)))

    meta = {
        "chunkIds": [str(x) for x in req.chunkIds],
        "chapterIds": [str(x) for x in req.chapterIds],
        "contents": req.contents,
        "indexedChapterIds": [str(x) for x in req.indexedChapterIds],
        "vectors": [[float(v) for v in row] for row in vectors.tolist()],
    }
    _write_meta(req.storyId, meta)
    logger.info("upsert completed storyId=%s chunkCount=%d dim=%d", req.storyId, n, dim)
    return {"ok": True, "chunkCount": n}


@app.post("/v1/index/delete-story")
def delete_story(req: StoryOnlyRequest, x_api_key: Optional[str] = Header(default=None)):
    _validate_key(x_api_key)
    logger.info("delete-story called storyId=%s", req.storyId)
    ip = _index_path(req.storyId)
    mp = _meta_path(req.storyId)
    if ip.exists():
        ip.unlink()
    if mp.exists():
        mp.unlink()
    return {"ok": True}


@app.get("/v1/index/has")
def has_index(storyId: UUID, x_api_key: Optional[str] = Header(default=None)):
    _validate_key(x_api_key)
    meta = _read_meta(storyId)
    has = _index_path(storyId).exists() and len(meta.get("chunkIds", [])) > 0
    logger.debug("has-index storyId=%s result=%s", storyId, has)
    return {"hasIndex": has}


@app.get("/v1/index/chunk-count")
def chunk_count(storyId: UUID, x_api_key: Optional[str] = Header(default=None)):
    _validate_key(x_api_key)
    meta = _read_meta(storyId)
    logger.debug("chunk-count storyId=%s count=%d", storyId, len(meta.get("chunkIds", [])))
    return {"chunkCount": len(meta.get("chunkIds", []))}


@app.get("/v1/index/indexed-chapters")
def indexed_chapters(storyId: UUID, x_api_key: Optional[str] = Header(default=None)):
    _validate_key(x_api_key)
    meta = _read_meta(storyId)
    ids = [UUID(x) for x in meta.get("indexedChapterIds", [])]
    return {"indexedChapterIds": ids}


@app.post("/v1/index/chunk-infos", response_model=ChunkInfosResponse)
def chunk_infos(req: ChunkInfosRequest, x_api_key: Optional[str] = Header(default=None)):
    _validate_key(x_api_key)
    if not req.chunkIds:
        return ChunkInfosResponse(items=[])
    meta = _read_meta(req.storyId)
    by_chunk = {}
    chunk_ids = meta.get("chunkIds", [])
    chapter_ids = meta.get("chapterIds", [])
    contents = meta.get("contents", [])
    for idx, cid in enumerate(chunk_ids):
        by_chunk[cid] = (
            chapter_ids[idx] if idx < len(chapter_ids) else None,
            contents[idx] if idx < len(contents) else "",
        )

    items = []
    for cid in req.chunkIds:
        raw = by_chunk.get(str(cid))
        if not raw or raw[0] is None:
            continue
        items.append(ChunkInfoItem(chunkId=cid, chapterId=UUID(raw[0]), content=raw[1] or ""))
    return ChunkInfosResponse(items=items)


@app.get("/v1/index/full", response_model=FullIndexResponse)
def full_index(storyId: UUID, x_api_key: Optional[str] = Header(default=None)):
    _validate_key(x_api_key)
    meta = _read_meta(storyId)
    return FullIndexResponse(
        ids=[UUID(x) for x in meta.get("chunkIds", [])],
        chapterIds=[UUID(x) for x in meta.get("chapterIds", [])],
        vectors=meta.get("vectors", []),
        contents=meta.get("contents", []),
    )


@app.post("/v1/index/search", response_model=SearchResponse)
def search(req: SearchRequest, x_api_key: Optional[str] = Header(default=None)):
    _validate_key(x_api_key)
    logger.info("search called storyId=%s topK=%d queryDim=%d", req.storyId, req.topK, len(req.queryVector))
    if req.topK <= 0:
        logger.info("search skipped storyId=%s reason=invalid_topK", req.storyId)
        return SearchResponse(results=[])
    ip = _index_path(req.storyId)
    if not ip.exists():
        logger.info("search skipped storyId=%s reason=index_not_found", req.storyId)
        return SearchResponse(results=[])

    meta = _read_meta(req.storyId)
    chunk_ids = meta.get("chunkIds", [])
    if not chunk_ids:
        logger.info("search skipped storyId=%s reason=empty_meta", req.storyId)
        return SearchResponse(results=[])

    index = faiss.read_index(str(ip))
    if hasattr(index, "hnsw"):
        ef_search = HNSW_EF_SEARCH if HNSW_EF_SEARCH > 0 else 64
        index.hnsw.efSearch = ef_search
    q = np.array([req.queryVector], dtype="float32")
    if q.ndim != 2 or q.shape[1] != index.d:
        raise HTTPException(status_code=400, detail=f"queryVector dim mismatch. expected {index.d}.")
    q = _normalize(q)

    k = min(req.topK, len(chunk_ids))
    scores, idxs = index.search(q, k);
    results = []
    for i, score in zip(idxs[0].tolist(), scores[0].tolist()):
        if i < 0 or i >= len(chunk_ids):
            continue
        results.append(SearchItem(chunkId=UUID(chunk_ids[i]), score=float(score)))
    logger.info("search completed storyId=%s returned=%d", req.storyId, len(results))
    return SearchResponse(results=results)

