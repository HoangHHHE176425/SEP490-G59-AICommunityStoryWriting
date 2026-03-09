# Sequence Diagram – Quản lý thể loại truyện

Admin tổ chức lại cấu trúc kho truyện: **Thêm / Sửa / Xóa** thể loại. Luồng từ Admin → Admin FE (Category Management) → Backend API (CategoriesController) → Category Service → Database.

**File Draw.io:** Mở file **[sequence-diagram-category-management.drawio](./sequence-diagram-category-management.drawio)** trong [draw.io](https://app.diagrams.net/) (hoặc VS Code với extension Draw.io Integration) để chỉnh sửa. File có 3 trang: *1. Thêm thể loại*, *2. Sửa thể loại*, *3. Xóa thể loại (an toàn)*.

---

## Tổng quan luồng

- **Admin:** Người dùng vai trò ADMIN, thao tác trên giao diện quản lý thể loại.
- **Admin FE (Category Management):** Trang `pages/admin/category/CategoryManagement.jsx`, gọi `categoryApi` (createCategory, updateCategory, deleteCategory).
- **Categories API:** Controller `api/categories` – POST (tạo), PUT `{id}` (sửa), DELETE `{id}` (xóa). Yêu cầu `[Authorize(Roles = "ADMIN")]`.
- **Category Service:** `CategoryService` – tạo slug, kiểm tra truyện trước khi xóa, ghi DB.
- **Database:** Bảng `categories`, `story_categories` (quan hệ nhiều-nhiều truyện–thể loại).

---

## 1. Thêm thể loại mới

Admin nhập Tên, Mô tả, Icon (tùy chọn), bật/tắt trạng thái. Backend tạo slug tự động từ tên (ví dụ: "Tiên Hiệp" → "tien-hiep"), đảm bảo slug duy nhất.

```mermaid
sequenceDiagram
    participant Admin
    participant AFE as Admin FE Category Management
    participant API as Categories API Controller
    participant Svc as Category Service
    participant DB as Database

    Admin->>+AFE: Thêm thể loại Tên Mô tả Icon IsActive
    AFE->>AFE: Validate tên không trống file icon max 2MB
    AFE->>+API: POST api categories FormData
    API->>API: Authorize ADMIN SaveIconFile ra IconUrl
    API->>+Svc: Create CreateCategoryRequestDto
    Svc->>Svc: BuildUniqueSlug Name ra slug
    Svc->>+DB: INSERT INTO categories
    DB-->>-Svc: OK
    Svc-->>-API: CategoryResponseDto
    API-->>-AFE: 201 Created category
    AFE->>AFE: loadCategories showToast Tạo thể loại thành công
    AFE-->>-Admin: Hiển thị danh sách cập nhật
```

---

## 2. Sửa thể loại

Admin chọn thể loại → Sửa. Có thể đổi tên (slug được tạo lại duy nhất), mô tả, icon, trạng thái active.

```mermaid
sequenceDiagram
    participant Admin
    participant AFE as Admin FE Category Management
    participant API as Categories API Controller
    participant Svc as Category Service
    participant DB as Database

    Admin->>+AFE: Sửa thể loại chọn thể loại đổi Tên Mô tả Icon IsActive
    AFE->>+API: PUT api categories id FormData
    API->>API: Authorize ADMIN GetById SaveIconFile nếu đổi icon
    API->>+Svc: Update id UpdateCategoryRequestDto
    Svc->>Svc: BuildUniqueSlug Name idToExclude ra slug mới
    Svc->>+DB: UPDATE categories SET name slug description icon_url is_active
    DB-->>-Svc: OK
    Svc-->>-API: true
    API-->>-AFE: 204 No Content
    AFE->>AFE: loadCategories showToast Cập nhật thành công
    AFE-->>-Admin: Danh sách cập nhật
```

---

## 3. Xóa thể loại (an toàn – kiểm tra truyện)

Backend **không cho xóa** thể loại nếu còn truyện đang gán thể loại đó; trả lỗi để FE hiển thị yêu cầu di chuyển truyện trước.

```mermaid
sequenceDiagram
    participant Admin
    participant AFE as Admin FE Category Management
    participant API as Categories API Controller
    participant Svc as Category Service
    participant DB as Database

    Admin->>AFE: Yêu cầu xóa thể loại chọn thể loại bấm Xóa
    activate AFE
    AFE->>AFE: confirm Bạn có chắc chắn muốn xóa thể loại này
    AFE->>API: DELETE api categories id
    activate API
    API->>API: Authorize ADMIN
    API->>Svc: Delete id
    activate Svc
    Svc->>DB: GetById id
    activate DB
    DB-->>Svc: category
    deactivate DB
    Svc->>DB: GetStoryCountByCategoryId truy vấn story_categories
    activate DB
    DB-->>Svc: storyCount
    deactivate DB
    alt Còn truyện storyCount lớn hơn 0
        Svc-->>API: throw InvalidOperationException
        API-->>AFE: 400 Bad Request
        AFE-->>Admin: Hiển thị lỗi Vui lòng di chuyển truyện trước khi xóa
    else Thể loại trống storyCount bằng 0
        API->>API: DeleteIconFile nếu có
        Svc->>DB: DELETE FROM categories qua Repository
        DB-->>Svc: OK
        Svc-->>API: true
        API-->>AFE: 204 No Content
        AFE->>AFE: loadCategories cập nhật danh sách
        AFE-->>Admin: Xóa thành công danh sách cập nhật
    end
    deactivate Svc
    deactivate API
    deactivate AFE
```

---

## Ghi chú kỹ thuật

| Thành phần | Dự án |
|------------|--------|
| **FE** | `pages/admin/category/CategoryManagement.jsx`, `api/category/categoryApi.jsx` (createCategory, updateCategory, deleteCategory). |
| **API** | `AIStory.API/Controllers/CategoriesController.cs` – route `api/categories`, POST/PUT/DELETE. |
| **Service** | `Services/Implementations/CategoryService.cs` – Create (BuildUniqueSlug), Update, Delete (GetStoryCountByCategoryId → ném lỗi nếu > 0). |
| **DB** | Bảng `categories`; bảng trung gian `story_categories` (story_id, category_id) để đếm truyện theo thể loại. |

Lưu ý: Trong code FE hiện tại, `handleDeleteCategory` chỉ cập nhật state local (filter) mà chưa gọi `deleteCategory(id)` từ API. Để luồng xóa đúng như sơ đồ, cần sửa để gọi `deleteCategory(category.id)`, bắt 400 và hiển thị `message` từ server cho user.
