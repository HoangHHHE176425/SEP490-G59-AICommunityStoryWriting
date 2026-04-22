# FAISS Service (Remote Vector Store)

Service này cung cấp FAISS qua HTTP để backend .NET gọi từ `FaissRemoteVectorStore`.

## Run local

```bash
pip install -r requirements.txt
uvicorn main:app --host 0.0.0.0 --port 8085
```

Optional env:

- `FAISS_DATA_DIR` (default `./data/faiss`)
- `FAISS_API_KEY` (nếu set, backend phải gửi header `X-Api-Key`)
- `FAISS_HNSW_M` (default `32`)
- `FAISS_HNSW_EF_CONSTRUCTION` (default `200`)
- `FAISS_HNSW_EF_SEARCH` (default `64`)

## Index type

Service hiện dùng `IndexHNSWFlat` với `METRIC_INNER_PRODUCT` (kết hợp normalize vector) để truy hồi nhanh và scale tốt hơn `IndexFlatIP`.

## Endpoints used by .NET

- `POST /v1/index/upsert`
- `POST /v1/index/delete-story`
- `GET /v1/index/has`
- `GET /v1/index/chunk-count`
- `GET /v1/index/indexed-chapters`
- `POST /v1/index/chunk-infos`
- `GET /v1/index/full`
- `POST /v1/index/search`
- `GET /health`

## .NET config

Trong `AIStory.API/appsettings*.json`:

```json
{
  "VectorStore": {
    "Provider": "FaissRemote"
  },
  "FaissService": {
    "BaseUrl": "http://localhost:8085",
    "TimeoutSeconds": 15,
    "ApiKey": ""
  }
}
```

Giữ `VectorStore:Provider = Local` nếu muốn dùng store cũ `FaissVectorStore`.
