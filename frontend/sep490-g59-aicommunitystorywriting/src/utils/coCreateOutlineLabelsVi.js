/**
 * Đổi nhãn tiếng Anh trong dàn ý do AI trả về sang tiếng Việt (chỉ hiển thị FE).
 */
export function translateCoCreateOutlineLabels(text) {
    if (text == null || text === '') return text;
    if (typeof text !== 'string') return text;

    let t = text;
    // Thứ tự: bản có markdown ** trước, rồi bản có dấu hai chấm
    const replacements = [
        [/\*\*Scene Objective\*\*\s*:/gi, '**Mục tiêu cảnh**:'],
        [/\*\*Scene Outline\*\*\s*:/gi, '**Dàn ý cảnh**:'],
        [/\*\*Characters Involved\*\*\s*:/gi, '**Nhân vật tham gia**:'],
        [/\*\*Potential Conflict\*\*\s*:/gi, '**Xung đột tiềm ẩn**:'],
        [/\*\*Expected Outcome\*\*\s*:/gi, '**Kết quả dự kiến**:'],
        [/Scene Objective\s*:/gi, 'Mục tiêu cảnh:'],
        [/Scene Outline\s*:/gi, 'Dàn ý cảnh:'],
        [/Characters Involved\s*:/gi, 'Nhân vật tham gia:'],
        [/Potential Conflict\s*:/gi, 'Xung đột tiềm ẩn:'],
        [/Expected Outcome\s*:/gi, 'Kết quả dự kiến:'],
    ];
    for (const [re, vi] of replacements) {
        t = t.replace(re, vi);
    }
    return t;
}
