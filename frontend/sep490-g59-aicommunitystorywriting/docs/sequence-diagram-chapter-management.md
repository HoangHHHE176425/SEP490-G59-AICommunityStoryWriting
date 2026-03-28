# Sequence Diagrams – Chapter Management

This document describes how **chapters** are listed, created, updated, deleted, submitted for review, and read. It maps flows to **`ChaptersController`**, **`ChapterService`**, and related code.

**Moderator flows** (claim, hàng đợi, duyệt/từ chối, escalation, lịch sử): see **[`sequence-diagram-moderator-moderation.md`](./sequence-diagram-moderator-moderation.md)**.

**Source of truth:** `AIStory.API/Controllers/ChaptersController.cs`, `Services/Implementations/ChapterService.cs`.

---

## How each section is organized

Every flow below follows the same layout:

1. **Summary** — what the user / client does.
2. **API** — HTTP method, path, auth.
3. **Chapter Service (step-by-step)** — ordered backend operations (matches how you break down **Create chapter**).
4. **Response** — status code and DTO / side effects.
5. **Sequence diagram** — Mermaid overview.

---

## Flow overview

- **Author:** `AUTHOR` role. UI: **My Stories** → `ChapterListManager.jsx`, `ChapterEditorPage.jsx`, `AuthorStoryManagement.jsx`.
- **Reader:** `ChapterReader.jsx`; list may be anonymous; **single chapter by id** requires login.
- **Moderator:** luồng duyệt nội dung và claim — **[`sequence-diagram-moderator-moderation.md`](./sequence-diagram-moderator-moderation.md)**.
- **Persistence:** `chapters`, `stories` (`total_chapters`, `word_count`, `last_published_at` updated in service paths below).

---

## 1. List chapters

### Summary

Author opens the chapter list for a story. Frontend calls `getChapters({ storyId, page, pageSize, ... })`.

### API

| Item | Value |
|------|--------|
| Method / path | `GET /api/chapters` |
| Auth | `[AllowAnonymous]` |
| Query | `ChapterQueryDto` (e.g. `storyId`, `status`, `page`, `pageSize`, filters) |

### Chapter Service (step-by-step)

1. **`GetAll(ChapterQueryDto query)`** — build `IQueryable` over chapters.
2. **Filter by `StoryId`** (when provided).
3. **Filter by status** — e.g. `Status`, `StatusIn`, or `PendingVersionChapterIds` for moderator-style pending lists.
4. **Filter by `AccessType`, search, sort** — `SortBy` / `SortOrder` (`order_index`, `created_at`, `published_at`, `title`, …).
5. **`Count()`** — total for pagination.
6. **`Skip` / `Take`** — page slice.
7. **Map each row** → **`ChapterListItemDto`** (via `MapToListItemDto`).
8. **`EnrichChapterListItemsWithReviewSla`** — SLA / claim fields for pending review.
9. **`EnrichModeratorRejectionHistoryForChapterList`** — rejection history on list items when applicable.
10. **Return** **`PagedResultDto<ChapterListItemDto>`** — `Items`, `TotalCount`, `Page`, `PageSize`.

### Response

- **`200 OK`** — body: **`PagedResultDto<ChapterListItemDto>`**.

### Sequence diagram

```mermaid
sequenceDiagram
    participant Author
    participant FE as ChapterListManager
    participant API as Chapters API Controller
    participant Svc as Chapter Service
    participant DB as Database

    Author->>+FE: Open chapter list for story
    FE->>+API: GET /api/chapters?storyId=...&page=1&pageSize=10
    API->>API: AllowAnonymous
    API->>+Svc: GetAll(ChapterQueryDto)
    Svc->>Svc: Build IQueryable over chapters
    Svc->>Svc: Filter by StoryId when provided
    Svc->>Svc: Filter by Status StatusIn or PendingVersionChapterIds
    Svc->>Svc: Filter AccessType search text SortBy SortOrder
    Svc->>+DB: Count total for pagination
    DB-->>-Svc: totalCount
    Svc->>+DB: Skip Take page slice
    DB-->>-Svc: chapter rows
    Svc->>Svc: Map each row to ChapterListItemDto
    Svc->>Svc: EnrichChapterListItemsWithReviewSla
    Svc->>Svc: EnrichModeratorRejectionHistoryForChapterList
    Svc-->>-API: PagedResultDto of ChapterListItemDto
    API-->>-FE: 200 OK
    FE->>FE: setChapters setTotalCount setTotalPages
    FE-->>-Author: Render chapter table
```

---

## 2. Create chapter (Author)

### Summary

Author creates a chapter (client-generated chapter `id` / UUID), fills title, content, order, access type, price, then saves. Backend persists one row in **`chapters`**.

### API

| Item | Value |
|------|--------|
| Method / path | `POST /api/chapters` |
| Auth | `[Authorize(Roles = "AUTHOR")]` |
| Body | **`CreateChapterRequestDto`** (JSON, `[FromBody]`) |

### Chapter Service (step-by-step)

1. **`request.Id` must be non-empty `Guid`** — otherwise throw **`ArgumentException`**.
2. **`StoryDAO.GetById(request.StoryId)`** — **story must exist**; else **`InvalidOperationException`** (“Story … not found”).
3. **`UserDAO.IsAuthorWritingSuspended(authorId)`** — if suspended, throw **`InvalidOperationException`**.
4. **`EnsureStoryProgressAllowsChapterWrite(story, …)`** — if story progress is **`HIATUS`** / **`COMPLETED`**, throw **`InvalidOperationException`**.
5. **`GetByStoryIdAndOrderIndex(StoryId, OrderIndex)`** — **must be no existing chapter** at that index (slot free); else **`InvalidOperationException`** (“order index … already exists”).
6. **`EnsureUniqueChapterTitleForStory(StoryId, Title, exclude: null)`** — title must not duplicate another chapter in the same story.
7. **Validate `AccessType`** — `FREE` or `PAID`; normalize default **`FREE`**.
8. **If `PAID`:** **`coin_price` > 0**; **`story.total_views` ≥ 500** — else **`ArgumentException`** / **`InvalidOperationException`** per code.
9. **If `FREE`:** force **`coin_price = 0`** when needed.
10. **Resolve content** — optionally fill from **`ai_generated_content`** when **`AiGeneratedContentId`** is set and output matches story.
11. **Compute `word_count`** from content.
12. **Normalize `Status`** from request — allowed: `DRAFT`, `PENDING_REVIEW`, `REJECTED`, `PUBLISHED`, `HIDDEN`, `ARCHIVED`; default **`DRAFT`**; if `PUBLISHED`, set **`published_at`** on the new row.
13. **`ChapterRepository.Add(chapter)`** — persist with **`status`** = **`DRAFT`** or **`PENDING_REVIEW`** / other valid value from request (typically **`DRAFT`** or **`PENDING_REVIEW`** from UI).
14. **`_aiContentRepository.BindDraftChapterId`** / **`UpdateChapterId`** — link AI draft rows when applicable.
15. **`UpdateStoryChapterStats(storyId)`** — recompute **`stories.total_chapters`**, **`stories.word_count`** (sum of chapter `word_count`), **`stories.updated_at`** via **`StoryDAO.Update`**.
16. **If new chapter `status == PUBLISHED`** (unusual on create): update **`story.last_published_at`**, **`StoryDAO.Update(story)`**, **`NotificationDAO.NotifyStoryFollowersNewChapter`** (and related), SignalR push as implemented.
17. **Return** **`ChapterResponseDto`** (`MapToResponseDto`).

### Response

- **`201 Created`** — body: **`ChapterResponseDto`**; **`Location`** header to resource.
- **`400 Bad Request`** — validation / business rule failures.
- **Controller (after create):** **`TriggerPlotManagerUpdate`** (async) if content is non-empty — does not block the HTTP response.

### Sequence diagram

```mermaid
sequenceDiagram
    participant Author
    participant FE as ChapterEditorPage
    participant API as Chapters API Controller
    participant Svc as Chapter Service
    participant DB as Database
    participant PM as PlotManagerBG

    Author->>+FE: Create chapter fill form Save Submit
    FE->>FE: Client validation min words PAID price
    FE->>+API: POST /api/chapters AUTHOR CreateChapterRequestDto JSON
    API->>+Svc: Create(request)
    Svc->>Svc: Assert request.Id is non-empty Guid
    Svc->>+DB: StoryDAO.GetById(StoryId)
    DB-->>-Svc: story row or null
    alt Story null or any rule throws
        Svc-->>API: InvalidOperationException or ArgumentException
        API-->>FE: 400 Bad Request
    else Story exists proceed
        Svc->>Svc: UserDAO.IsAuthorWritingSuspended(author_id)
        Svc->>Svc: EnsureStoryProgressAllowsChapterWrite(story)
        Svc->>+DB: GetByStoryIdAndOrderIndex(StoryId OrderIndex)
        DB-->>-Svc: no row slot free or duplicate
        Svc->>Svc: EnsureUniqueChapterTitleForStory(StoryId Title)
        Svc->>Svc: Validate AccessType FREE or PAID
        Svc->>Svc: If PAID require coin_price greater than 0 and story total_views at least 500
        Svc->>Svc: If FREE force coin_price zero when needed
        Svc->>+DB: Optional load AiGeneratedContent by AiGeneratedContentId
        DB-->>-Svc: ai row or skip
        Svc->>Svc: Merge ai_output into content if applicable
        Svc->>Svc: Calculate word_count from content
        Svc->>Svc: Normalize Status DRAFT default set published_at if PUBLISHED
        Svc->>+DB: ChapterRepository.Add(new chapter row)
        DB-->>-Svc: inserted
        Svc->>+DB: AiContentRepo.BindDraftChapterId and UpdateChapterId if ids set
        DB-->>-Svc: OK
        Svc->>+DB: UpdateStoryChapterStats then StoryDAO.Update total_chapters word_count updated_at
        DB-->>-Svc: OK
        opt New chapter status is PUBLISHED rare
            Svc->>+DB: Story last_published_at NotifyStoryFollowersNewChapter
            DB-->>-Svc: OK
        end
        Svc-->>API: ChapterResponseDto MapToResponseDto
        API->>PM: TriggerPlotManagerUpdate if content non-empty
        API-->>FE: 201 Created body
    end
    FE->>FE: Navigate toast reload list
    FE-->>-Author: Updated UI
```

---

## 3. Update chapter (Author)

### Summary

Author loads a chapter, edits fields, saves. May set status to **`PENDING_REVIEW`** (submit for review) via **`PUT`**.

### API

| Item | Value |
|------|--------|
| Load | `GET /api/chapters/{id}` — `[Authorize]` |
| Save | `PUT /api/chapters/{id}` — `[Authorize(Roles = "AUTHOR")]`, **`UpdateChapterRequestDto`** |

### Chapter Service (step-by-step)

1. **`ChapterRepository.GetById(id)`** — chapter **must exist**; else **`Update`** returns false → **404**.
2. **If `OrderIndex` changes** — **`GetByStoryIdAndOrderIndex`** must not point to **another** chapter id; else **`InvalidOperationException`**.
3. **`EnsureUniqueChapterTitleForStory`** when title changes.
4. **Validate `AccessType` / `CoinPrice`** — same PAID rules as create when switching to or staying PAID.
5. **If `request.Status` → `PENDING_REVIEW`:** **`UserDAO.IsAuthorWritingSuspended`**, **`EnsureCanSubmitForReview(chapter)`** (sequential chapter rules).
6. **`ChapterRepository.Update(chapter)`** — apply title, content, status, `submitted_for_review_at` / `published_at` transitions per status change.
7. **`UpdateStoryChapterStats(storyId)`** — refresh **`stories.total_chapters`**, **`stories.word_count`**.
8. **If status becomes `PUBLISHED`** (from update path): **`story.last_published_at`**, notifications to followers as in service.
9. **Return** **`true`** / **`false`**.

### Response

- **`204 No Content`** — update succeeded.
- **`404 Not Found`** — chapter id unknown.
- **`400 Bad Request`** — business validation.

### Sequence diagram

```mermaid
sequenceDiagram
    participant Author
    participant FE as ChapterEditor AuthorStoryManagement
    participant API as Chapters API Controller
    participant Svc as Chapter Service
    participant DB as Database
    participant PM as PlotManagerBG

    Author->>+FE: Click Edit DRAFT or REJECTED
    FE->>+API: GET /api/chapters/{id} Authorize
    API->>+Svc: GetById(id)
    Svc->>+DB: ChapterRepository.GetById
    DB-->>-Svc: chapter entity
    Svc->>Svc: MapToResponseDto attach rejection if REJECTED
    Svc-->>-API: ChapterResponseDto
    API-->>-FE: 200 OK
    FE->>FE: Bind form open editor
    Author->>FE: Save or set PENDING_REVIEW
    FE->>+API: PUT /api/chapters/{id} AUTHOR UpdateChapterRequestDto
    API->>+Svc: Update(id request)
    Svc->>+DB: ChapterRepository.GetById(id)
    DB-->>-Svc: chapter or null
    alt Chapter null
        Svc-->>API: false
        API-->>FE: 404 Not Found
    else Chapter found
        opt Request OrderIndex differs from stored
            Svc->>+DB: GetByStoryIdAndOrderIndex(new index)
            DB-->>-Svc: other chapter or none
            Svc->>Svc: Throw if other chapter id different
        end
        Svc->>Svc: EnsureUniqueChapterTitleForStory if title changed
        Svc->>Svc: Validate AccessType and CoinPrice PAID rules
        opt New status PENDING_REVIEW
            Svc->>Svc: UserDAO.IsAuthorWritingSuspended
            Svc->>Svc: EnsureCanSubmitForReview prior chapter approved or rejected
        end
        Svc->>+DB: ChapterRepository.Update fields status timestamps
        DB-->>-Svc: OK
        Svc->>+DB: UpdateStoryChapterStats StoryDAO.Update
        DB-->>-Svc: OK
        opt Status became PUBLISHED
            Svc->>+DB: Story last_published_at NotifyStoryFollowersNewChapter
            DB-->>-Svc: OK
        end
        Svc-->>API: true
        API->>PM: TriggerPlotManagerUpdate if content changed
        API-->>FE: 204 No Content
    end
    FE->>FE: Return to list toast
    FE-->>-Author: Updated UI
```

---

## 4. Delete chapter (Author)

### Summary

Hard delete of a **DRAFT** chapter. If **`chapter_versions`** exist, first call may return **409** until user confirms **`deleteIncludingVersions=true`**.

### API

| Item | Value |
|------|--------|
| Method / path | `DELETE /api/chapters/{id}?deleteIncludingVersions={bool}` |
| Auth | `[Authorize(Roles = "AUTHOR")]` |

### Chapter Service (step-by-step)

1. **`GetById(id)`** — if null → return false (**404**).
2. **Chapter `status` must be `DRAFT`** — else **`InvalidOperationException`**.
3. **Count versions** — if **> 0** and **`deleteIncludingVersions == false`** → throw with **`ErrorCode = CHAPTER_DELETE_VERSIONS_CONFIRM_REQUIRED`** (**409**).
4. **`ReviewAssignmentDAO.CompleteAssignment(CHAPTER, id)`** (best effort).
5. **`_aiContentRepository.DeleteAllByChapterId(id)`**.
6. If versions: **`DeleteAllByChapterId`** on version repository.
7. **`ChapterRepository.Delete(id)`**.
8. **`UpdateStoryChapterStats(storyId)`** — update **`total_chapters`**, **`word_count`** on story.

### Response

- **`204 No Content`** — deleted.
- **`404`** — not found.
- **`409 Conflict`** — versions present; client must retry with **`deleteIncludingVersions=true`**.

### Sequence diagram

```mermaid
sequenceDiagram
    participant Author
    participant FE as ChapterListManager
    participant API as Chapters API Controller
    participant Svc as Chapter Service
    participant DB as Database

    Author->>+FE: Delete chapter DRAFT only
    FE->>FE: Confirm dialog
    FE->>+API: DELETE /api/chapters/{id} deleteIncludingVersions=false
    API->>+Svc: Delete(id deleteIncludingVersions=false)
    Svc->>+DB: ChapterRepository.GetById(id)
    DB-->>-Svc: chapter or null
    alt Chapter null
        Svc-->>API: false
        API-->>FE: 404 Not Found
    else Chapter status is not DRAFT
        Svc-->>API: InvalidOperationException
        API-->>FE: 400 Bad Request
    else Version count greater than zero AND deleteIncludingVersions is false
        Svc-->>API: InvalidOperationException ErrorCode CHAPTER_DELETE_VERSIONS_CONFIRM_REQUIRED
        API-->>FE: 409 Conflict versionCount in body
        FE->>+API: DELETE /api/chapters/{id} deleteIncludingVersions=true
        API->>+Svc: Delete(id deleteIncludingVersions=true)
        Svc->>Svc: ReviewAssignmentDAO.CompleteAssignment TargetTypeChapter id
        Svc->>+DB: AiContentRepo.DeleteAllByChapterId(id)
        DB-->>-Svc: OK
        Svc->>+DB: VersionRepo.DeleteAllByChapterId(id)
        DB-->>-Svc: OK
        Svc->>+DB: ChapterRepository.Delete(id)
        DB-->>-Svc: OK
        Svc->>+DB: UpdateStoryChapterStats StoryDAO.Update story totals
        DB-->>-Svc: OK
        Svc-->>API: true
        API-->>FE: 204 No Content
    else First call allowed chapter has zero versions
        Svc->>Svc: ReviewAssignmentDAO.CompleteAssignment TargetTypeChapter id
        Svc->>+DB: AiContentRepo.DeleteAllByChapterId(id)
        DB-->>-Svc: OK
        Svc->>+DB: ChapterRepository.Delete(id)
        DB-->>-Svc: OK
        Svc->>+DB: UpdateStoryChapterStats StoryDAO.Update story totals
        DB-->>-Svc: OK
        Svc-->>API: true
        API-->>FE: 204 No Content
    end
    FE->>FE: loadChapters()
    FE-->>-Author: List refreshed
```

---

## 5. Submit chapter for review (Author — “Publish” in UI)

### Summary

Author submits chapter for moderation → chapter **`PENDING_REVIEW`**. Either **`PUT`** with status or **`POST .../publish`**.

### API

| Path A | `PUT /api/chapters/{id}` with `Status = PENDING_REVIEW` |
| Path B | `POST /api/chapters/{id}/publish` → **`ChapterService.Publish`** |
| Optional | `PUT /api/stories/{storyId}` — story also **`PENDING_REVIEW`** |

### Chapter Service — Path A (`Update`)

1. Same as **§3** when **`newStatus == PENDING_REVIEW`**: suspension, **`EnsureCanSubmitForReview`**, no conflicting pending **version** on same chapter.
2. **`ChapterRepository.Update`** — `status`, **`submitted_for_review_at`**, etc.
3. **`UpdateStoryChapterStats`**.

### Chapter Service — Path B (`Publish`)

1. **`GetById`** — must exist.
2. **`UserDAO.IsAuthorWritingSuspended`**, **`EnsureStoryProgressAllowsChapterWrite`**.
3. **No version in `PENDING_REVIEW`** for this chapter.
4. **`EnsureCanSubmitForReview(chapter)`**.
5. **`chapter.status = PENDING_REVIEW`**, **`submitted_for_review_at = UtcNow`**.
6. **`ChapterRepository.Update`**.
7. **`_moderationHubNotifier.NotifyPendingListChangedAsync`**.

### Response

- **`204 No Content`** — typical success for publish/unpublish endpoints.
- **`400 Bad Request`** — rule violations.

### Sequence diagram

```mermaid
sequenceDiagram
    participant Author
    participant FE as ChapterListManager
    participant ChAPI as Chapters API
    participant StAPI as Stories API
    participant ChSvc as Chapter Service
    participant StSvc as Story Service
    participant DB as Database
    participant Hub as Moderation hub notifier

    Author->>+FE: Submit chapter for review from DRAFT or REJECTED
    FE->>FE: Confirm dialog
    opt Load full title and content from server
        FE->>+ChAPI: GET /api/chapters/{id}
        ChAPI->>ChSvc: GetById
        ChSvc->>+DB: ChapterRepository.GetById
        DB-->>-ChSvc: chapter row
        ChSvc-->>ChAPI: ChapterResponseDto
        ChAPI-->>-FE: 200 OK
    end
    alt Option A PUT chapter with status PENDING_REVIEW
        FE->>+ChAPI: PUT /api/chapters/{id} UpdateChapterRequestDto
        ChAPI->>+ChSvc: Update(id request)
        ChSvc->>+DB: ChapterRepository.GetById(id)
        DB-->>-ChSvc: chapter
        ChSvc->>+DB: StoryDAO.GetById(story_id)
        DB-->>-ChSvc: story
        ChSvc->>ChSvc: EnsureStoryProgressAllowsChapterWrite(story)
        ChSvc->>ChSvc: If OrderIndex changed GetByStoryIdAndOrderIndex no duplicate slot
        ChSvc->>ChSvc: EnsureUniqueChapterTitleForStory(storyId title exclude id)
        ChSvc->>ChSvc: Validate AccessType CoinPrice PAID min views 500 when switching to PAID
        opt Request.Status becomes PENDING_REVIEW
            ChSvc->>ChSvc: UserDAO.IsAuthorWritingSuspended(author_id)
            ChSvc->>ChSvc: EnsureCanSubmitForReview(chapter)
            ChSvc->>ChSvc: Set submitted_for_review_at when entering PENDING_REVIEW
        end
        ChSvc->>+DB: ChapterRepository.Update fields word_count etc
        DB-->>-ChSvc: OK
        ChSvc->>+DB: UpdateStoryChapterStats
        DB-->>-ChSvc: OK
        Note over ChSvc: PUT Update does not call moderation hub notifier
        ChSvc-->>ChAPI: true
        ChAPI-->>FE: 204 No Content
    else Option B POST author publish endpoint
        FE->>+ChAPI: POST /api/chapters/{id}/publish
        ChAPI->>+ChSvc: Publish(id)
        ChSvc->>+DB: ChapterRepository.GetById(id)
        DB-->>-ChSvc: chapter
        ChSvc->>ChSvc: UserDAO.IsAuthorWritingSuspended(author_id)
        ChSvc->>ChSvc: EnsureStoryProgressAllowsChapterWrite(story)
        ChSvc->>ChSvc: Query versions any PENDING_REVIEW for chapter id else throw
        ChSvc->>ChSvc: EnsureCanSubmitForReview(chapter)
        ChSvc->>ChSvc: Assign status PENDING_REVIEW submitted_for_review_at UtcNow
        ChSvc->>+DB: ChapterRepository.Update
        DB-->>-ChSvc: OK
        ChSvc->>Hub: NotifyPendingListChangedAsync
        ChSvc-->>ChAPI: true
        ChAPI-->>FE: 204 No Content
    end
    opt Optional sync story to PENDING_REVIEW
        FE->>+StAPI: PUT /api/stories/{storyId}
        StAPI->>+StSvc: Update(storyId dto)
        StSvc->>+DB: StoryDAO.Update
        DB-->>-StSvc: OK
        StSvc-->>StAPI: true
        StAPI-->>-FE: 204 No Content
    end
    FE->>FE: loadChapters refresh pending flags
    FE-->>-Author: Chapter shows pending review
```

---

## 6. Unsubmit chapter (Author)

### API

`POST /api/chapters/{id}/unpublish` — **`ChapterService.Unpublish`**.

### Chapter Service (step-by-step)

1. **`GetById`** — null → false (**404**).
2. **`ReviewAssignmentDAO.IsLocked(CHAPTER, id)`** — if locked → **`InvalidOperationException`** (**400**).
3. **`EnsureCanUnpublish(chapter)`** — reverse order vs higher-index chapters still pending/published.
4. **`chapter.status = DRAFT`**, clear **`submitted_for_review_at`**.
5. **`ChapterRepository.Update`**.
6. **`ReviewAssignmentDAO.CompleteAssignment(CHAPTER, id)`**.
7. **`NotifyPendingListChangedAsync`**.

### Response

- **`204 No Content`** on success.
- **`400`** / **`404`** as above.

### Sequence diagram

```mermaid
sequenceDiagram
    participant Author
    participant FE as ChapterListManager
    participant API as Chapters API Controller
    participant Svc as Chapter Service
    participant DB as Database
    participant Hub as Moderation hub notifier

    Author->>+FE: Unsubmit chapter PENDING_REVIEW
    FE->>FE: Confirm
    FE->>+API: POST /api/chapters/{id}/unpublish AUTHOR
    API->>+Svc: Unpublish(id)
    Svc->>+DB: ChapterRepository.GetById(id)
    DB-->>-Svc: chapter row or null
    alt Chapter missing
        Svc-->>API: false
        API-->>FE: 404 Not Found
    else Chapter exists
        Svc->>+DB: ReviewAssignmentDAO.IsLocked CHAPTER id
        DB-->>-Svc: locked flag
        alt Moderator locked claim
            Svc-->>API: InvalidOperationException
            API-->>FE: 400 Bad Request
        else Not locked
            Svc->>Svc: EnsureCanUnpublish(chapter)
            Note over Svc: Reverse order vs higher-index chapters still pending or published
            alt Rule violated
                Svc-->>API: InvalidOperationException
                API-->>FE: 400 Bad Request
            else OK
                Svc->>Svc: chapter.status DRAFT clear submitted_for_review_at
                Svc->>+DB: ChapterRepository.Update(chapter)
                DB-->>-Svc: OK
                Svc->>+DB: ReviewAssignmentDAO.CompleteAssignment CHAPTER id
                DB-->>-Svc: OK
                Svc->>Hub: NotifyPendingListChangedAsync
                Svc-->>API: true
                API-->>FE: 204 No Content
            end
        end
    end
    API-->>-FE: Response
    FE->>FE: loadChapters()
    FE-->>-Author: Back to draft when successful
```

---

## 7. Reorder chapters (Author)

### API

`POST /api/chapters/{id}/reorder` — body **`newOrderIndex`** (JSON number).

### Chapter Service (step-by-step)

1. **`GetById(id)`** — null → false (**404**).
2. **`GetByStoryIdAndOrderIndex(storyId, newOrderIndex)`** — if another chapter occupies index, **swap** `order_index` between the two; else assign **`newOrderIndex`** to current chapter.
3. **`ChapterRepository.Update`** (one or two rows).

### Response

- **`204 No Content`**.

### Sequence diagram

```mermaid
sequenceDiagram
    participant Author
    participant FE as ChapterListManager
    participant API as Chapters API Controller
    participant Svc as Chapter Service
    participant DB as Database

    Author->>+FE: Change order if UI supports it
    FE->>+API: POST /api/chapters/{id}/reorder AUTHOR body newOrderIndex
    API->>+Svc: Reorder(id newOrderIndex)
    Svc->>+DB: ChapterRepository.GetById(id)
    DB-->>-Svc: chapter or null
    alt Chapter not found
        Svc-->>API: false
        API-->>FE: 404 Not Found
    else OK
        Svc->>+DB: GetByStoryIdAndOrderIndex(storyId newOrderIndex)
        DB-->>-Svc: other chapter at index or empty
        alt Slot occupied by another chapter
            Svc->>Svc: Swap order_index between current and other
            Svc->>+DB: ChapterRepository.Update both rows
            DB-->>-Svc: OK
        else Slot free
            Svc->>Svc: Assign newOrderIndex to current chapter
            Svc->>+DB: ChapterRepository.Update current row
            DB-->>-Svc: OK
        end
        Svc-->>API: true
        API-->>FE: 204 No Content
    end
    API-->>-FE: Response
    FE->>FE: loadChapters()
    FE-->>-Author: Updated order
```

---

## 8. Read chapter (Reader)

### API

- `GET /api/stories/{storyId}` — story metadata.
- `GET /api/chapters/{chapterId}` — **`[Authorize]`**; paid unlock, compliance, **`RecordReadChapter`** when allowed.
- `GET /api/chapters?storyId=...&status=PUBLISHED&...` — sidebar list.

### ChaptersController + ChapterService (step-by-step for GET by id)

1. **`ChapterService.GetById(id)`** — map to **`ChapterResponseDto`**; attach rejection fields if **`REJECTED`**.
2. Load **story** via **`StoryService.GetById`** — **compliance hidden** rules for non-author.
3. **Paid chapter:** if not author and not unlocked → strip **`Content`**, set **`IsUnlocked`**.
4. Else **record read** — **`StoryService.RecordReadChapter`**.
5. Return **`200 OK`**.

### Response

- **`200 OK`** — **`ChapterResponseDto`** (content may be null when locked).

### Sequence diagram

```mermaid
sequenceDiagram
    participant Reader
    participant FE as ChapterReader
    participant StAPI as Stories API
    participant ChAPI as Chapters API
    participant ChSvc as Chapter Service
    participant StSvc as Story Service
    participant DB as Database

    Reader->>+FE: Open reader route storyId chapterId
    par Parallel loads
        FE->>+StAPI: GET /api/stories/{storyId}
        StAPI->>+StSvc: GetById
        StSvc->>+DB: StoryDAO.GetById
        DB-->>-StSvc: story
        StSvc-->>StAPI: StoryResponseDto compliance fields
        StAPI-->>-FE: 200 OK
    and
        FE->>+ChAPI: GET /api/chapters/{chapterId} Authorize
        ChAPI->>+ChSvc: GetById(id)
        ChSvc->>+DB: ChapterRepository.GetById
        DB-->>-ChSvc: chapter row
        ChSvc-->>-ChAPI: ChapterResponseDto or null
        alt Chapter not found
            ChAPI-->>FE: 404 Not Found
        else Found
            ChAPI->>+StSvc: GetById(storyId userId) story metadata compliance
            StSvc->>+DB: StoryDAO.GetById
            DB-->>-StSvc: story
            StSvc-->>-ChAPI: StoryResponseDto or null
            alt Story missing or compliance hidden for user
                ChAPI-->>FE: 404 Not Found
            else Story visible
                ChAPI->>ChAPI: Resolve isAuthor PAID unlock HasUnlockedPaidChapter
                alt PAID locked not author
                    ChAPI->>ChAPI: Content null WordCount null IsUnlocked false
                    ChAPI-->>FE: 200 OK metadata only
                else FREE or unlocked or author
                    opt Logged-in user
                        ChAPI->>+StSvc: RecordReadChapter(storyId chapterId userId ip ua)
                        StSvc->>+DB: Persist read analytics
                        DB-->>-StSvc: OK
                        StSvc-->>-ChAPI: OK
                    end
                    ChAPI-->>FE: 200 OK full or partial DTO
                end
            end
        end
    and
        FE->>+ChAPI: GET /api/chapters storyId status PUBLISHED paging
        ChAPI->>+ChSvc: GetAll filters sort paging
        ChSvc->>+DB: Query chapters Count Skip Take
        DB-->>-ChSvc: rows
        ChSvc-->>ChAPI: PagedResultDto ChapterListItemDto
        ChAPI-->>-FE: 200 OK
    end
    FE->>FE: setStory setChapter sidebar list
    FE-->>-Reader: Render reader
```

---

## 9. Chapter versions (Author) — endpoint summary

| Action | Method | Route |
|--------|--------|--------|
| List | GET | `/api/chapters/{chapterId}/versions` |
| Detail | GET | `/api/chapters/{chapterId}/versions/{versionId}` |
| Create | POST | `/api/chapters/{chapterId}/versions` |
| Update | PUT | `/api/chapters/{chapterId}/versions/{versionId}` |
| Delete | DELETE | `/api/chapters/{chapterId}/versions/{versionId}` |
| Submit | POST | `/api/chapters/{chapterId}/versions/{versionId}/submit` |
| Unsubmit | POST | `/api/chapters/{chapterId}/versions/{versionId}/unsubmit` |

See **`ChapterVersionService`** for step-by-step rules (author match, DRAFT-only edits, single pending version, sequential rules, story progress locks).

---

## Conditions checklist (compact)

| Flow | Key rules |
|------|-----------|
| List | Anonymous OK; returns **`PagedResultDto<ChapterListItemDto>`**. |
| Create | Story exists; **free `order_index`**; unique title; PAID + views; **`UpdateStoryChapterStats`**; optional **`last_published_at`** if **`PUBLISHED`**. |
| Update | Exists; order/title/access rules; **`PENDING_REVIEW`** → suspension + sequential; **`UpdateStoryChapterStats`**. |
| Delete | **`DRAFT` only**; **409** if versions without confirm. |
| Submit | **`PENDING_REVIEW`** via **`PUT`** or **`Publish`**; hub notify. |
| Unpublish | Not locked; reverse order. |
| Mod (toàn bộ) | **[`sequence-diagram-moderator-moderation.md`](./sequence-diagram-moderator-moderation.md)** — claim, hàng đợi, duyệt/từ chối, escalation. |

---

## API endpoints reference (`ChaptersController`)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/chapters` | AUTHOR | Create chapter (JSON body) |
| GET | `/api/chapters` | AllowAnonymous | List → **`PagedResultDto<ChapterListItemDto>`** |
| GET | `/api/chapters/{id}` | **Authorize** | Get by id |
| GET | `/api/chapters/story/{storyId}` | AllowAnonymous | By story |
| GET | `/api/chapters/story/{storyId}/order/{orderIndex}` | AllowAnonymous | By story + order |
| PUT | `/api/chapters/{id}` | AUTHOR | Update |
| DELETE | `/api/chapters/{id}` | AUTHOR | Delete draft (`deleteIncludingVersions`) |
| POST | `/api/chapters/{id}/publish` | AUTHOR | Submit → **`PENDING_REVIEW`** |
| POST | `/api/chapters/{id}/unpublish` | AUTHOR | Back to **`DRAFT`** |
| POST | `/api/chapters/{id}/reorder` | AUTHOR | Reorder |
| GET | `/api/chapters/{id}/rejection-reason` | AUTHOR | Rejection info |
| * | `/api/chapters/{chapterId}/versions/...` | AUTHOR | Versions |

Moderator: xem **[`sequence-diagram-moderator-moderation.md`](./sequence-diagram-moderator-moderation.md)** (đầy đủ route `api/moderator` và escalation admin).
