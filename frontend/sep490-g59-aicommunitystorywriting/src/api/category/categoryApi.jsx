import axiosInstance from "../axiosInstance";

/**
 * Tạo thể loại mới.
 * @param {Object} data - { name (required), description?, isActive?, parentId?, iconImage? (File) }
 * @returns {Promise} - Created category từ server
 * @throws {Error} Nếu name không được cung cấp hoặc rỗng
 */
export async function createCategory(data) {
    // Validate name (required, max 100 chars theo schema)
    const name = (data.name || "").trim();
    if (!name) {
        throw new Error("Tên thể loại không được để trống");
    }
    if (name.length > 100) {
        throw new Error("Tên thể loại không được vượt quá 100 ký tự");
    }

    const formData = new FormData();

    // Name: NVARCHAR(100) NOT NULL
    formData.append("Name", name);

    // Description: NVARCHAR(MAX) - nullable, trim nếu có
    const description = (data.description || "").trim();
    if (description) {
        formData.append("Description", description);
    } else {
        formData.append("Description", "");
    }

    // IsActive: BIT DEFAULT 1 - convert boolean to string for FormData
    formData.append("IsActive", String(data.isActive !== false));

    // ParentId: UNIQUEIDENTIFIER NULL - chỉ gửi nếu có giá trị hợp lệ
    if (data.parentId != null && data.parentId !== "" && data.parentId !== "null" && data.parentId !== "undefined") {
        formData.append("ParentId", data.parentId);
    }

    // IconImage: File - chỉ gửi nếu là File object hợp lệ và có extension hợp lệ
    if (data.iconImage instanceof File) {
        // Validate file extension (backend requires: jpg, jpeg, png, gif, webp, svg)
        const allowedExtensions = ['.jpg', '.jpeg', '.png', '.gif', '.webp', '.svg'];
        const fileName = data.iconImage.name.toLowerCase();
        const fileExtension = fileName.substring(fileName.lastIndexOf('.'));
        
        if (!allowedExtensions.includes(fileExtension)) {
            throw new Error(`Định dạng file không hợp lệ. Chỉ chấp nhận: ${allowedExtensions.join(', ').toUpperCase()}`);
        }
        
        // Validate file size (max 2MB)
        if (data.iconImage.size > 2 * 1024 * 1024) {
            throw new Error('Kích thước ảnh không được vượt quá 2MB');
        }
        
        formData.append("IconImage", data.iconImage);
    }

    const response = await axiosInstance.post("/categories", formData, {
        headers: {
            "Content-Type": "multipart/form-data",
        },
    });
    return response.data;
}

/**
 * Lấy tất cả thể loại.
 * @param {Object} params - { includeInactive?: boolean, parentId?: string (Guid), rootsOnly?: boolean }
 * @returns {Promise} - Danh sách categories từ server
 */
export async function getAllCategories(params = {}) {
    const queryParams = new URLSearchParams();
    if (params.includeInactive !== undefined) {
        queryParams.append("includeInactive", params.includeInactive);
    }
    if (params.parentId != null) {
        queryParams.append("parentId", params.parentId);
    }
    if (params.rootsOnly !== undefined) {
        queryParams.append("rootsOnly", params.rootsOnly);
    }

    const queryString = queryParams.toString();
    const url = queryString ? `/categories?${queryString}` : "/categories";

    const response = await axiosInstance.get(url);
    return response.data;
}

/**
 * Lấy thể loại theo ID.
 * @param {string} id - Guid của category
 * @returns {Promise} - Category từ server
 */
export async function getCategoryById(id) {
    const response = await axiosInstance.get(`/categories/${id}`);
    return response.data;
}

/**
 * Lấy thể loại theo slug.
 * @param {string} slug - Slug của category
 * @returns {Promise} - Category từ server
 */
export async function getCategoryBySlug(slug) {
    const response = await axiosInstance.get(`/categories/slug/${slug}`);
    return response.data;
}

/**
 * Cập nhật thể loại.
 * @param {string} id - Guid của category cần cập nhật
 * @param {Object} data - { name (required), description?, isActive?, parentId?, iconImage? (File) }
 * @returns {Promise} - Response từ server (NoContent nếu thành công)
 * @throws {Error} Nếu name không được cung cấp hoặc rỗng
 */
export async function updateCategory(id, data) {
    // Validate name (required, max 100 chars theo schema)
    const name = (data.name || "").trim();
    if (!name) {
        throw new Error("Tên thể loại không được để trống");
    }
    if (name.length > 100) {
        throw new Error("Tên thể loại không được vượt quá 100 ký tự");
    }

    const formData = new FormData();

    // Name: NVARCHAR(100) NOT NULL
    formData.append("Name", name);

    // Description: NVARCHAR(MAX) - nullable, trim nếu có
    const description = (data.description || "").trim();
    if (description) {
        formData.append("Description", description);
    } else {
        formData.append("Description", "");
    }

    // IsActive: BIT DEFAULT 1 - convert boolean to string for FormData
    formData.append("IsActive", String(data.isActive !== false));

    // ParentId: UNIQUEIDENTIFIER NULL - chỉ gửi nếu có giá trị hợp lệ
    if (data.parentId != null && data.parentId !== "" && data.parentId !== "null" && data.parentId !== "undefined") {
        formData.append("ParentId", data.parentId);
    }

    // IconImage: File - chỉ gửi nếu là File object hợp lệ và có extension hợp lệ
    if (data.iconImage instanceof File) {
        // Validate file extension (backend requires: jpg, jpeg, png, gif, webp, svg)
        const allowedExtensions = ['.jpg', '.jpeg', '.png', '.gif', '.webp', '.svg'];
        const fileName = data.iconImage.name.toLowerCase();
        const fileExtension = fileName.substring(fileName.lastIndexOf('.'));
        
        if (!allowedExtensions.includes(fileExtension)) {
            throw new Error(`Định dạng file không hợp lệ. Chỉ chấp nhận: ${allowedExtensions.join(', ').toUpperCase()}`);
        }
        
        // Validate file size (max 2MB)
        if (data.iconImage.size > 2 * 1024 * 1024) {
            throw new Error('Kích thước ảnh không được vượt quá 2MB');
        }
        
        formData.append("IconImage", data.iconImage);
    }

    const response = await axiosInstance.put(`/categories/${id}`, formData, {
        headers: {
            "Content-Type": "multipart/form-data",
        },
    });
    return response.data;
}

/**
 * Xóa thể loại.
 * @param {string} id - Guid của category cần xóa
 * @returns {Promise} - Response từ server (NoContent nếu thành công)
 */
export async function deleteCategory(id) {
    const response = await axiosInstance.delete(`/categories/${id}`);
    return response.data;
}

/**
 * Bật/tắt trạng thái active của thể loại.
 * @param {string} id - Guid của category
 * @returns {Promise} - Response từ server (NoContent nếu thành công)
 */
export async function toggleCategoryActive(id) {
    const response = await axiosInstance.patch(`/categories/${id}/toggle-active`);
    return response.data;
}
