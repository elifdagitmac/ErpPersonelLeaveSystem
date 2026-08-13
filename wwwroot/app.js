"use strict";

const API_BASE = "/api/personnel";

const LEAVE_TYPES = {
    1: { label: "Yıllık İzin (Ücretli)", badge: "badge-yillik" },
    2: { label: "Ücretsiz İzin (Kesintili)", badge: "badge-ucretsiz" },
    3: { label: "Sağlık İzni (Ücretli)", badge: "badge-saglik" },
    4: { label: "Mazeret İzni (Ücretli)", badge: "badge-mazaret" },
};

const WORK_STATUSES = {
    1: { label: "Şirkette / Ofiste", emoji: "🟢", badge: "badge-office" },
    2: { label: "Remote / Evden", emoji: "🔵", badge: "badge-remote" },
    3: { label: "İzinli", emoji: "🟡", badge: "badge-onleave" },
    4: { label: "Giriş Yapılmadı", emoji: "🔴", badge: "badge-noentry" },
};

let employeesCache = [];
let leavesCache = [];
let pendingLeavesCache = [];
let lastCalculatedResult = null;
let currentUser = null;
let currentCalDate = new Date();

/* ---------- Tema (Açık / Koyu) ---------- */

const THEME_STORAGE_KEY = "erp-theme";
const USER_STORAGE_KEY = "erp-current-user";

function applyTheme(theme) {
    if (theme === "light") {
        document.documentElement.setAttribute("data-theme", "light");
    } else {
        document.documentElement.removeAttribute("data-theme");
    }
}

function initTheme() {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    applyTheme(stored === "light" ? "light" : "dark");

    const themeToggleBtn = document.getElementById("themeToggle");
    if (themeToggleBtn) {
        themeToggleBtn.addEventListener("click", () => {
            const isLight = document.documentElement.getAttribute("data-theme") === "light";
            const next = isLight ? "dark" : "light";
            applyTheme(next);
            localStorage.setItem(THEME_STORAGE_KEY, next);
        });
    }
}

/* ---------- Yardımcı Fonksiyonlar ---------- */

function formatCurrency(value) {
    const number = Number(value || 0);
    return number.toLocaleString("tr-TR", { style: "currency", currency: "TRY" });
}

function formatDate(dateStr) {
    if (!dateStr) return "-";
    const d = new Date(dateStr);
    return isNaN(d.getTime()) ? "-" : d.toLocaleDateString("tr-TR");
}

function escapeHtml(str) {
    const div = document.createElement("div");
    div.textContent = str ?? "";
    return div.innerHTML;
}

function showToast(message, type) {
    const toast = document.getElementById("toast");
    if (!toast) {
        if (type === "error") alert(message);
        return;
    }
    toast.textContent = message;
    toast.className = "toast show" + (type ? " " + type : "");
    clearTimeout(showToast._timer);
    showToast._timer = setTimeout(() => {
        toast.classList.remove("show");
    }, 3500);
}

function setStatus(elementId, message, type) {
    const el = document.getElementById(elementId);
    if (!el) return;
    el.textContent = message || "";
    el.className = "status-msg" + (type ? " " + type : "");
}

async function apiFetch(url, options) {
    try {
        const response = await fetch(url, options);
        if (!response.ok) {
            let detail = "";
            try {
                const data = await response.json();
                detail = data?.Message || data?.error || data?.message || JSON.stringify(data);
            } catch {
                detail = await response.text();
            }
            throw new Error(detail || `İstek başarısız oldu (HTTP ${response.status}).`);
        }
        if (response.status === 204) return null;
        const text = await response.text();
        return text ? JSON.parse(text) : null;
    } catch (err) {
        console.error("API İletişim Hatası:", err);
        throw err;
    }
}

/* ---------- Kullanıcı Girişi (Auth & RBAC) ---------- */

function initAuth() {
    const loginForm = document.getElementById("loginForm");
    const loginModal = document.getElementById("loginModal");
    const closeLoginBtn = document.getElementById("closeLoginModalBtn");

    if (closeLoginBtn && loginModal) {
        closeLoginBtn.addEventListener("click", () => {
            loginModal.style.display = "none";
            loginModal.hidden = true;
        });
    }

    const storedUser = localStorage.getItem(USER_STORAGE_KEY);

    if (storedUser) {
        try {
            currentUser = JSON.parse(storedUser);
            if (loginModal) {
                loginModal.style.display = "none";
                loginModal.hidden = true;
            }
            applyUserPermissions();
        } catch {
            localStorage.removeItem(USER_STORAGE_KEY);
            currentUser = null;
            if (loginModal) loginModal.style.display = "flex";
        }
    } else {
        currentUser = null;
        if (loginModal) loginModal.style.display = "flex";
    }

    if (loginForm) {
        loginForm.addEventListener("submit", async (e) => {
            e.preventDefault();

            const nameInp = document.getElementById("loginName");
            const empIdInp = document.getElementById("loginEmpId");

            const name = (nameInp && nameInp.value ? nameInp.value.trim() : "") || "test testtest";
            const empId = Number(empIdInp && empIdInp.value ? empIdInp.value : 1) || 1;

            currentUser = {
                Id: empId,
                Name: name,
                Department: empId === 1 ? "IT" : "Yazılım",
                Role: empId === 1 ? "Admin" : "Employee",
                WorkStatus: 1
            };

            localStorage.setItem(USER_STORAGE_KEY, JSON.stringify(currentUser));

            if (loginModal) {
                loginModal.style.display = "none";
                loginModal.hidden = true;
            }

            applyUserPermissions();
            showToast(`🔑 Hoş geldiniz, ${currentUser.Name}!`, "success");

            try {
                await loadEmployees();
                await loadLeaves();
                await loadPendingLeaves();
                renderCalendarGrid();
            } catch (err) {
                console.warn("Arka plan verileri bekleniyor:", err);
            }
        });
    }
}

function applyUserPermissions() {
    if (!currentUser) return;

    const isAdmin = currentUser.Role === "Admin";

    const addEmpBtn = document.getElementById("openAddEmployeeModal");
    if (addEmpBtn) addEmpBtn.style.display = isAdmin ? "" : "none";

    const uploadBtn = document.getElementById("uploadCsvBtn");
    if (uploadBtn) uploadBtn.style.display = isAdmin ? "inline-flex" : "none";

    const pendingTabBtn = document.getElementById("pendingTabBtn");
    if (pendingTabBtn) pendingTabBtn.style.display = isAdmin ? "" : "none";

    let userBadge = document.getElementById("userProfileBadge");
    const topbarActions = document.querySelector(".topbar-actions");

    if (!userBadge && topbarActions) {
        userBadge = document.createElement("div");
        userBadge.id = "userProfileBadge";
        userBadge.className = "user-badge";
        userBadge.style.display = "inline-flex";
        userBadge.style.alignItems = "center";
        userBadge.style.gap = "8px";
        topbarActions.insertBefore(userBadge, topbarActions.firstChild);
    }

    if (userBadge) {
        userBadge.innerHTML = `
            <span class="badge ${isAdmin ? 'badge-office' : 'badge-remote'}" style="padding: 6px 12px; font-weight: 600;">
                <i class="fa-solid ${isAdmin ? 'fa-user-shield' : 'fa-user'}"></i> 
                ${escapeHtml(currentUser.Name)} (${isAdmin ? 'Yönetici' : 'Çalışan'})
            </span>
            <button id="logoutBtn" class="btn btn-sm btn-secondary" title="Çıkış Yap" style="padding: 6px 10px;">
                <i class="fa-solid fa-right-from-bracket"></i> Çıkış
            </button>
        `;

        document.getElementById("logoutBtn")?.addEventListener("click", () => {
            localStorage.removeItem(USER_STORAGE_KEY);
            location.reload();
        });
    }
}

/* ---------- Toplu Excel / CSV Personel Dosyası Yükleme ---------- */

function initCsvUpload() {
    const uploadBtn = document.getElementById("uploadCsvBtn");
    const fileInput = document.getElementById("csvFileInput");

    if (uploadBtn && fileInput) {
        uploadBtn.addEventListener("click", () => fileInput.click());

        fileInput.addEventListener("change", async () => {
            const file = fileInput.files[0];
            if (!file) return;

            const formData = new FormData();
            formData.append("file", file);

            showToast("⏳ Excel / CSV dosyası işleniyor...", "loading");

            try {
                const response = await fetch(`${API_BASE}/import-csv`, {
                    method: "POST",
                    body: formData
                });

                const data = await response.json();
                if (!response.ok) throw new Error(data.Message || "Yükleme başarısız oldu.");

                showToast(data.Message || "✅ Personel verileri veritabanına aktarıldı!", "success");
                alert(data.Message || "✅ Personel verileri veritabanına aktarıldı!");
                fileInput.value = ""; // Temizle

                await loadEmployees();
                await loadPendingLeaves();
                await loadLeaves();
                renderCalendarGrid();
            } catch (err) {
                showToast("Yükleme Hatası: " + err.message, "error");
                alert("Yükleme Hatası: " + err.message);
            }
        });
    }
}

/* ---------- Excel İzin Dökümü İndirme ---------- */

function initExportLeaves() {
    const btn = document.getElementById("exportLeavesCsvBtn");
    if (btn) {
        btn.addEventListener("click", () => {
            showToast("📊 Excel izin dökümü indiriliyor...", "loading");
            window.location.href = `${API_BASE}/export-leaves-csv`;
        });
    }
}

/* ---------- Departman Görsel İzin Takvimi ---------- */

function initCalendar() {
    const prevBtn = document.getElementById("prevMonthBtn");
    const nextBtn = document.getElementById("nextMonthBtn");
    const deptFilter = document.getElementById("calDeptFilter");

    if (prevBtn) {
        prevBtn.addEventListener("click", () => {
            currentCalDate.setMonth(currentCalDate.getMonth() - 1);
            renderCalendarGrid();
        });
    }

    if (nextBtn) {
        nextBtn.addEventListener("click", () => {
            currentCalDate.setMonth(currentCalDate.getMonth() + 1);
            renderCalendarGrid();
        });
    }

    if (deptFilter) {
        deptFilter.addEventListener("change", renderCalendarGrid);
    }
}

function renderCalendarGrid() {
    const grid = document.getElementById("calendarGrid");
    const label = document.getElementById("currentMonthYearLabel");
    if (!grid) return;

    grid.innerHTML = "";

    const year = currentCalDate.getFullYear();
    const month = currentCalDate.getMonth();

    const monthNames = ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];
    if (label) label.textContent = `${monthNames[month]} ${year}`;

    const firstDayIndex = new Date(year, month, 1).getDay();
    const startDay = firstDayIndex === 0 ? 6 : firstDayIndex - 1; // Pazartesi = 0
    const totalDaysInMonth = new Date(year, month + 1, 0).getDate();

    const selectedDept = document.getElementById("calDeptFilter")?.value || "";

    // Boş başlangıç kareleri
    for (let x = 0; x < startDay; x++) {
        const emptyCell = document.createElement("div");
        emptyCell.style.background = "rgba(255, 255, 255, 0.02)";
        emptyCell.style.borderRadius = "8px";
        emptyCell.style.minHeight = "90px";
        grid.appendChild(emptyCell);
    }

    // Gün kareleri
    for (let day = 1; day <= totalDaysInMonth; day++) {
        const dateObj = new Date(year, month, day);
        const dayCell = document.createElement("div");
        dayCell.className = "glass";
        dayCell.style.padding = "8px";
        dayCell.style.borderRadius = "8px";
        dayCell.style.minHeight = "95px";
        dayCell.style.display = "flex";
        dayCell.style.flexDirection = "column";
        dayCell.style.gap = "4px";
        dayCell.style.border = "1px solid rgba(255,255,255,0.08)";

        const isToday = new Date().toDateString() === dateObj.toDateString();
        if (isToday) {
            dayCell.style.borderColor = "var(--primary-color, #6366f1)";
            dayCell.style.background = "rgba(99, 102, 241, 0.15)";
        }

        const dateHeader = document.createElement("div");
        dateHeader.style.fontWeight = "700";
        dateHeader.style.fontSize = "0.9rem";
        dateHeader.style.marginBottom = "4px";
        dateHeader.textContent = day;
        dayCell.appendChild(dateHeader);

        // İzin kayıtlarını bu güne eşleştir
        const allLeaves = [...leavesCache, ...pendingLeavesCache];
        const dayLeaves = allLeaves.filter(r => {
            const start = new Date(r.startDate || r.StartDate);
            const end = new Date(r.endDate || r.EndDate);
            start.setHours(0, 0, 0, 0);
            end.setHours(23, 59, 59, 999);

            const matchDate = dateObj >= start && dateObj <= end;
            const matchDept = !selectedDept || (r.department || (r.employee ? r.employee.department : '')) === selectedDept;

            return matchDate && matchDept;
        });

        dayLeaves.forEach(r => {
            const pill = document.createElement("div");
            const leaveTypeVal = Number(r.leaveType !== undefined ? r.leaveType : r.LeaveType);
            const typeInfo = LEAVE_TYPES[leaveTypeVal] ?? { label: "İzin", badge: "badge-yillik" };
            const empName = r.employeeName || (r.employee ? r.employee.name : "Personel");

            pill.className = `badge ${typeInfo.badge}`;
            pill.style.fontSize = "10px";
            pill.style.padding = "2px 5px";
            pill.style.whiteSpace = "nowrap";
            pill.style.overflow = "hidden";
            pill.style.textOverflow = "ellipsis";
            pill.style.borderRadius = "4px";
            pill.title = `${empName} (${typeInfo.label})`;
            pill.textContent = `${empName.split(' ')[0]}`;

            dayCell.appendChild(pill);
        });

        grid.appendChild(dayCell);
    }
}

/* ---------- Sekme Geçişleri ---------- */

function initTabs() {
    const buttons = document.querySelectorAll(".tab-btn");
    buttons.forEach((btn) => {
        btn.addEventListener("click", () => {
            buttons.forEach((b) => b.classList.remove("active"));
            document.querySelectorAll(".tab-panel").forEach((p) => p.classList.remove("active"));

            btn.classList.add("active");
            const targetId = btn.dataset.tab;
            const targetPanel = document.getElementById(targetId);
            if (targetPanel) targetPanel.classList.add("active");

            if (targetId === "tab-explorer") {
                loadLeaves();
            } else if (targetId === "tab-pending") {
                loadPendingLeaves();
            } else if (targetId === "tab-calendar") {
                renderCalendarGrid();
            }
        });
    });
}

/* ---------- KPI Kartları ---------- */

function renderKpis() {
    const totalEmployees = employeesCache.length;
    const inOffice = employeesCache.filter((e) => Number(e.workStatus !== undefined ? e.workStatus : e.WorkStatus) === 1).length;
    const totalPayroll = employeesCache.reduce((sum, e) => sum + Number(e.monthlySalary !== undefined ? e.monthlySalary : (e.MonthlySalary || 0)), 0);

    const elTotal = document.getElementById("kpiTotalEmployees");
    const elOffice = document.getElementById("kpiInOffice");
    const elPayroll = document.getElementById("kpiTotalPayroll");

    const isAdmin = currentUser?.Role === "Admin";

    if (elTotal) elTotal.textContent = totalEmployees;
    if (elOffice) elOffice.textContent = inOffice;

    if (elPayroll) {
        elPayroll.textContent = isAdmin ? formatCurrency(totalPayroll) : "🔒 Gizli";
    }
}

function renderLeaveKpi(leaveRecords) {
    const totalDays = leaveRecords.reduce((sum, r) => sum + Number(r.leaveDays !== undefined ? r.leaveDays : (r.LeaveDays || 0)), 0);
    const elLeave = document.getElementById("kpiTotalLeaveDays");
    if (elLeave) elLeave.textContent = totalDays + " Gün";
}

/* ---------- Personel Listesi ---------- */

async function loadEmployees() {
    setStatus("listStatus", "Personel listesi yükleniyor...", "loading");
    try {
        const employees = await apiFetch(API_BASE);
        employeesCache = employees || [];
        renderEmployeeTable(employeesCache);
        populateSimEmployeeSelect(employeesCache);
        renderKpis();
        setStatus("listStatus", "", "");
    } catch (err) {
        setStatus("listStatus", "Personel listesi alınamadı: " + err.message, "error");
    }
}

function renderEmployeeTable(employees) {
    const tbody = document.getElementById("employeeTableBody");
    const emptyMsg = document.getElementById("emptyListMsg");
    if (!tbody) return;
    tbody.innerHTML = "";

    if (!employees.length) {
        if (emptyMsg) emptyMsg.hidden = false;
        return;
    }
    if (emptyMsg) emptyMsg.hidden = true;

    const isAdmin = currentUser?.Role === "Admin";

    for (const emp of employees) {
        const empId = emp.id !== undefined ? emp.id : emp.Id;
        const empName = emp.name !== undefined ? emp.name : emp.Name;
        const empDept = emp.department !== undefined ? emp.department : emp.Department;
        const empSalary = Number(emp.monthlySalary !== undefined ? emp.monthlySalary : emp.MonthlySalary);
        const statusVal = Number(emp.workStatus !== undefined ? emp.workStatus : emp.WorkStatus);
        const statusInfo = WORK_STATUSES[statusVal] ?? { label: "Bilinmiyor", emoji: "⚪", badge: "" };

        const canViewSalary = isAdmin || String(currentUser?.Id) === String(empId);
        const displaySalary = canViewSalary ? formatCurrency(empSalary) : "🔒 Gizli";

        const tr = document.createElement("tr");
        tr.innerHTML = `
            <td>#${empId}</td>
            <td><strong>${escapeHtml(empName)}</strong></td>
            <td>${escapeHtml(empDept)}</td>
            <td><span class="badge ${statusInfo.badge}">${statusInfo.emoji} ${escapeHtml(statusInfo.label)}</span></td>
            <td>${displaySalary}</td>
            <td>
                <div class="row-actions">
                    ${isAdmin ? `<button class="btn-icon" title="Düzenle" data-edit-emp="${empId}"><i class="fa-solid fa-pen"></i></button>` : ''}
                    ${isAdmin ? `<button class="btn-icon danger" title="Sil" data-delete-emp="${empId}"><i class="fa-solid fa-trash"></i></button>` : ''}
                    <button class="btn-icon" title="İzin Simülasyonu" data-select-emp="${empId}"><i class="fa-solid fa-bolt"></i></button>
                </div>
            </td>
        `;
        tbody.appendChild(tr);
    }

    tbody.querySelectorAll("[data-select-emp]").forEach((btn) => {
        btn.addEventListener("click", () => {
            const id = btn.getAttribute("data-select-emp");
            const simSelect = document.getElementById("simEmployee");
            if (simSelect) {
                simSelect.value = id;
                simSelect.scrollIntoView({ behavior: "smooth", block: "center" });
            }
        });
    });

    tbody.querySelectorAll("[data-edit-emp]").forEach((btn) => {
        btn.addEventListener("click", () => {
            const id = btn.getAttribute("data-edit-emp");
            const employee = employeesCache.find((e) => String(e.id !== undefined ? e.id : e.Id) === String(id));
            if (employee) openEmployeeModal(employee);
        });
    });

    tbody.querySelectorAll("[data-delete-emp]").forEach((btn) => {
        btn.addEventListener("click", () => deleteEmployee(btn.getAttribute("data-delete-emp")));
    });
}

function filterEmployeeTable() {
    const searchInp = document.getElementById("searchInput");
    if (!searchInp) return;
    const term = searchInp.value.trim().toLowerCase();
    if (!term) {
        renderEmployeeTable(employeesCache);
        return;
    }
    const filtered = employeesCache.filter(
        (e) =>
            (e.name || e.Name)?.toLowerCase().includes(term) ||
            (e.department || e.Department)?.toLowerCase().includes(term)
    );
    renderEmployeeTable(filtered);
}

async function deleteEmployee(id) {
    const employee = employeesCache.find((e) => String(e.id !== undefined ? e.id : e.Id) === String(id));
    const name = employee ? (employee.name || employee.Name) : `#${id}`;
    if (!confirm(`"${name}" adlı personeli silmek istediğinize emin misiniz? Bu işlem geri alınamaz.`)) {
        return;
    }

    try {
        await apiFetch(`${API_BASE}/${id}`, { method: "DELETE" });
        showToast("Personel başarıyla silindi.", "success");
        await loadEmployees();
    } catch (err) {
        showToast("Personel silinemedi: " + err.message, "error");
    }
}

/* ---------- Personel Ekle / Düzenle Modalı ---------- */

function openEmployeeModal(employee) {
    const form = document.getElementById("employeeForm");
    if (form) form.reset();
    setStatus("employeeFormStatus", "", "");

    const isEdit = Boolean(employee);
    const empIdEl = document.getElementById("empId");
    if (empIdEl) empIdEl.value = isEdit ? (employee.id !== undefined ? employee.id : employee.Id) : "";

    const titleEl = document.getElementById("employeeModalTitle");
    if (titleEl) {
        titleEl.innerHTML = isEdit
            ? '<i class="fa-solid fa-pen"></i> Personel Düzenle'
            : '<i class="fa-solid fa-user-plus"></i> Yeni Personel Ekle';
    }

    const submitBtn = document.getElementById("submitEmployeeBtn");
    if (submitBtn) {
        submitBtn.innerHTML = isEdit
            ? '<i class="fa-solid fa-floppy-disk"></i> Güncelle'
            : '<i class="fa-solid fa-floppy-disk"></i> Kaydet';
    }

    const empEduField = document.getElementById("empEducationField");
    const empGenderField = document.getElementById("empGenderField");
    const empEdu = document.getElementById("empEducation");
    const empGender = document.getElementById("empGender");
    const empEditNote = document.getElementById("empEditNote");

    if (empEduField) empEduField.style.display = isEdit ? "none" : "";
    if (empGenderField) empGenderField.style.display = isEdit ? "none" : "";
    if (empEdu) empEdu.required = !isEdit;
    if (empGender) empGender.required = !isEdit;
    if (empEditNote) empEditNote.hidden = !isEdit;

    if (isEdit) {
        document.getElementById("empName").value = employee.name || employee.Name || "";
        document.getElementById("empDepartment").value = employee.department || employee.Department || "";
        document.getElementById("empExperience").value = employee.experienceYears !== undefined ? employee.experienceYears : (employee.ExperienceYears || 0);
        document.getElementById("empAge").value = employee.age !== undefined ? employee.age : (employee.Age || 25);
        document.getElementById("empSalary").value = employee.monthlySalary !== undefined ? employee.monthlySalary : (employee.MonthlySalary || 0);
        document.getElementById("empWorkStatus").value = employee.workStatus !== undefined ? employee.workStatus : (employee.WorkStatus || 1);
    } else {
        const wsEl = document.getElementById("empWorkStatus");
        if (wsEl) wsEl.value = 1;
    }

    const modal = document.getElementById("employeeModal");
    if (modal) modal.hidden = false;
}

function closeEmployeeModal() {
    const modal = document.getElementById("employeeModal");
    if (modal) modal.hidden = true;
}

function initEmployeeModal() {
    const openBtn = document.getElementById("openAddEmployeeModal");
    const closeBtn = document.getElementById("closeEmployeeModal");
    const cancelBtn = document.getElementById("cancelEmployeeModal");
    const modal = document.getElementById("employeeModal");
    const form = document.getElementById("employeeForm");

    if (openBtn) openBtn.addEventListener("click", () => openEmployeeModal(null));
    if (closeBtn) closeBtn.addEventListener("click", closeEmployeeModal);
    if (cancelBtn) cancelBtn.addEventListener("click", closeEmployeeModal);
    if (modal) {
        modal.addEventListener("click", (e) => {
            if (e.target.id === "employeeModal") closeEmployeeModal();
        });
    }

    if (form) {
        form.addEventListener("submit", async (e) => {
            e.preventDefault();

            const id = document.getElementById("empId").value;
            const isEdit = Boolean(id && id !== "0");

            const empEduEl = document.getElementById("empEducation");
            const empGenderEl = document.getElementById("empGender");

            const payload = {
                Name: document.getElementById("empName").value.trim(),
                Department: document.getElementById("empDepartment").value.trim(),
                ExperienceYears: Number(document.getElementById("empExperience").value) || 0,
                EducationLevel: isEdit ? "Lisans" : (empEduEl && empEduEl.value ? empEduEl.value : "Lisans"),
                Age: Number(document.getElementById("empAge").value) || 25,
                Gender: isEdit ? "Belirtilmedi" : (empGenderEl && empGenderEl.value ? empGenderEl.value : "Belirtilmedi"),
                MonthlySalary: Number(document.getElementById("empSalary").value) || 0,
                WorkStatus: Number(document.getElementById("empWorkStatus").value) || 1,
            };

            const submitBtn = document.getElementById("submitEmployeeBtn");
            if (submitBtn) submitBtn.disabled = true;
            setStatus("employeeFormStatus", isEdit ? "Güncelleniyor..." : "Kaydediliyor...", "loading");

            try {
                if (isEdit) {
                    await apiFetch(`${API_BASE}/${id}`, {
                        method: "PUT",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify(payload),
                    });
                    showToast("Personel bilgileri güncellendi.", "success");
                } else {
                    await apiFetch(API_BASE, {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify(payload),
                    });
                    showToast("Personel başarıyla eklendi.", "success");
                }

                closeEmployeeModal();
                await loadEmployees();
            } catch (err) {
                setStatus("employeeFormStatus", "İşlem başarısız: " + err.message, "error");
                showToast("İşlem başarısız oldu: " + err.message, "error");
            } finally {
                if (submitBtn) submitBtn.disabled = false;
            }
        });
    }
}

/* ---------- İzin & Maaş Simülatörü ve Tarih Hesaplayıcı ---------- */

function populateSimEmployeeSelect(employees) {
    const select = document.getElementById("simEmployee");
    if (!select) return;
    const currentValue = select.value;
    select.innerHTML = '<option value="" disabled selected>Personel seçiniz</option>';

    for (const emp of employees) {
        const empId = emp.id !== undefined ? emp.id : emp.Id;
        const empName = emp.name !== undefined ? emp.name : emp.Name;
        const empDept = emp.department !== undefined ? emp.department : emp.Department;
        const empSalary = Number(emp.monthlySalary !== undefined ? emp.monthlySalary : (emp.MonthlySalary || 0));

        const opt = document.createElement("option");
        opt.value = empId;
        opt.textContent = `${empName} — ${empDept} (${formatCurrency(empSalary)})`;
        select.appendChild(opt);
    }

    if (currentValue) select.value = currentValue;
}

function initDatePickers() {
    const startInp = document.getElementById("simStartDate");
    const endInp = document.getElementById("simEndDate");
    const daysInp = document.getElementById("simLeaveDays");

    const today = new Date().toISOString().split("T")[0];
    const tomorrow = new Date(Date.now() + 86400000).toISOString().split("T")[0];

    if (startInp && !startInp.value) startInp.value = today;
    if (endInp && !endInp.value) endInp.value = tomorrow;

    function calculateDays() {
        if (!startInp || !endInp || !daysInp) return;
        const start = new Date(startInp.value);
        const end = new Date(endInp.value);

        if (isNaN(start.getTime()) || isNaN(end.getTime())) return;

        const diffTime = end.getTime() - start.getTime();
        const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24)) + 1;

        daysInp.value = diffDays > 0 ? diffDays : 1;
    }

    if (startInp) startInp.addEventListener("change", calculateDays);
    if (endInp) endInp.addEventListener("change", calculateDays);
    calculateDays();
}

function initSimForm() {
    initDatePickers();

    const form = document.getElementById("simForm");
    const confirmBtn = document.getElementById("confirmLeaveBtn");

    if (form) {
        form.addEventListener("submit", async (e) => {
            e.preventDefault();

            const employeeId = document.getElementById("simEmployee").value;
            const leaveType = Number(document.getElementById("simLeaveType").value);
            const leaveDays = Number(document.getElementById("simLeaveDays").value);

            if (!employeeId) {
                setStatus("simStatus", "Lütfen bir personel seçin.", "error");
                return;
            }

            const employee = employeesCache.find((e) => String(e.id !== undefined ? e.id : e.Id) === String(employeeId));
            if (!employee) {
                setStatus("simStatus", "Seçilen personel bulunamadı, listeyi yenileyin.", "error");
                return;
            }

            const salaryVal = Number(employee.monthlySalary !== undefined ? employee.monthlySalary : (employee.MonthlySalary || 0));

            setStatus("simStatus", "Hesaplanıyor...", "loading");

            try {
                const query = new URLSearchParams({
                    monthlySalary: salaryVal,
                    leaveType: leaveType,
                    leaveDays: leaveDays,
                });

                const result = await apiFetch(`${API_BASE}/calculate?${query.toString()}`, {
                    method: "POST",
                });

                lastCalculatedResult = { ...result, employeeId, leaveType, leaveDays };
                renderResult(result, leaveType, leaveDays);
                setStatus("simStatus", "", "");
                if (confirmBtn) confirmBtn.disabled = false;
            } catch (err) {
                setStatus("simStatus", "Hesaplama başarısız: " + err.message, "error");
                const resCard = document.getElementById("resultCard");
                if (resCard) resCard.hidden = true;
            }
        });
    }

    if (confirmBtn) {
        confirmBtn.addEventListener("click", async () => {
            const employeeId = document.getElementById("simEmployee").value;
            const leaveType = Number(document.getElementById("simLeaveType").value);
            const startDate = document.getElementById("simStartDate").value;
            const endDate = document.getElementById("simEndDate").value;
            const leaveDays = Number(document.getElementById("simLeaveDays").value);
            const noteEl = document.getElementById("simNote");
            const note = noteEl ? noteEl.value.trim() : "";

            if (!employeeId) {
                showToast("Lütfen bir personel seçin.", "error");
                return;
            }

            confirmBtn.disabled = true;
            setStatus("simStatus", "İzin talebi gönderiliyor...", "loading");

            try {
                const payload = {
                    employeeId: Number(employeeId),
                    LeaveType: leaveType,
                    StartDate: startDate,
                    EndDate: endDate,
                    LeaveDays: leaveDays,
                    Note: note || null,
                };

                const result = await apiFetch(`${API_BASE}/request-leave`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(payload),
                });

                showToast(result?.Message || "⏳ İzin talebiniz başarıyla oluşturuldu ve Yöneticiye gönderildi!", "success");
                setStatus("simStatus", result?.Message || "İzin talebi oluşturuldu.", "success");

                if (noteEl) noteEl.value = "";
                const resCard = document.getElementById("resultCard");
                if (resCard) resCard.hidden = true;
                lastCalculatedResult = null;

                await loadPendingLeaves();
                renderCalendarGrid();
            } catch (err) {
                setStatus("simStatus", err.message, "error");
                showToast(err.message, "error");
            } finally {
                confirmBtn.disabled = false;
            }
        });
    }
}

function renderResult(result, leaveType, leaveDays) {
    const card = document.getElementById("resultCard");
    if (card) card.hidden = false;

    const setTxt = (id, txt) => {
        const el = document.getElementById(id);
        if (el) el.textContent = txt;
    };

    setTxt("resBaseSalary", formatCurrency(result.baseMonthlySalary));
    setTxt("resDailyWage", formatCurrency(result.dailyWage));
    setTxt("resLeaveType", LEAVE_TYPES[leaveType]?.label ?? leaveType);
    setTxt("resLeaveDays", leaveDays + " Gün");
    setTxt("resDeduction", "-" + formatCurrency(result.deductionAmount));
    setTxt("resFinalSalary", formatCurrency(result.finalNetSalary));
    setTxt("resFormula", result.formulaApplied || "");
}

/* ---------- Bekleyen İzin Talepleri & Onay Paneli (Yönetici Sekmesi) ---------- */

async function loadPendingLeaves() {
    if (currentUser?.Role !== "Admin") return;

    setStatus("pendingStatus", "Bekleyen talepler yükleniyor...", "loading");
    try {
        const records = await apiFetch(`${API_BASE}/pending-leaves`);
        pendingLeavesCache = records || [];
        renderPendingLeavesTable(pendingLeavesCache);
        updatePendingBadge(pendingLeavesCache.length);
        setStatus("pendingStatus", "", "");
    } catch (err) {
        setStatus("pendingStatus", "Bekleyen talepler alınamadı: " + err.message, "error");
    }
}

function updatePendingBadge(count) {
    const badge = document.getElementById("pendingBadge");
    if (!badge) return;
    if (count > 0) {
        badge.textContent = count;
        badge.hidden = false;
        badge.style.background = "#ef4444";
        badge.style.color = "#ffffff";
        badge.style.padding = "2px 8px";
        badge.style.borderRadius = "12px";
        badge.style.fontSize = "12px";
        badge.style.marginLeft = "6px";
    } else {
        badge.hidden = true;
    }
}

function renderPendingLeavesTable(records) {
    const tbody = document.getElementById("pendingLeavesTableBody");
    const emptyMsg = document.getElementById("emptyPendingMsg");
    if (!tbody) return;
    tbody.innerHTML = "";

    if (!records.length) {
        if (emptyMsg) emptyMsg.hidden = false;
        return;
    }
    if (emptyMsg) emptyMsg.hidden = true;

    for (const r of records) {
        const recId = r.id !== undefined ? r.id : r.Id;
        const leaveTypeVal = Number(r.leaveType !== undefined ? r.leaveType : r.LeaveType);
        const typeInfo = LEAVE_TYPES[leaveTypeVal] ?? { label: leaveTypeVal, badge: "" };
        const empName = r.employeeName || (r.employee ? r.employee.name : "Bilinmiyor");
        const empDept = r.department || (r.employee ? r.employee.department : "Genel");
        const startStr = formatDate(r.startDate || r.StartDate);
        const endStr = formatDate(r.endDate || r.EndDate);
        const leaveDaysVal = r.leaveDays !== undefined ? r.leaveDays : r.LeaveDays;
        const noteVal = r.note !== undefined ? r.note : r.Note;

        const tr = document.createElement("tr");
        tr.innerHTML = `
            <td>#${recId}</td>
            <td><strong>${escapeHtml(empName)}</strong></td>
            <td>${escapeHtml(empDept)}</td>
            <td><span class="badge ${typeInfo.badge}">${escapeHtml(typeInfo.label)}</span></td>
            <td>${startStr} — ${endStr}</td>
            <td><strong>${leaveDaysVal} Gün</strong></td>
            <td>${escapeHtml(noteVal || "-")}</td>
            <td>
                <div class="row-actions">
                    <button class="btn btn-sm btn-success" data-approve-leave="${recId}">
                        <i class="fa-solid fa-check"></i> Onayla
                    </button>
                    <button class="btn btn-sm btn-danger" data-reject-leave="${recId}" style="background: rgba(239,68,68,0.2); border: 1px solid #ef4444;">
                        <i class="fa-solid fa-xmark"></i> Reddet
                    </button>
                </div>
            </td>
        `;
        tbody.appendChild(tr);
    }

    tbody.querySelectorAll("[data-approve-leave]").forEach((btn) => {
        btn.addEventListener("click", () => approveLeaveRecord(btn.getAttribute("data-approve-leave")));
    });

    tbody.querySelectorAll("[data-reject-leave]").forEach((btn) => {
        btn.addEventListener("click", () => rejectLeaveRecord(btn.getAttribute("data-reject-leave")));
    });
}

async function approveLeaveRecord(id) {
    if (!confirm(`#${id} numaralı izin talebini onaylamak istediğinize emin misiniz? Maaş kesintisi ve PDKS durumu işlenecektir.`)) return;

    try {
        const result = await apiFetch(`${API_BASE}/approve-leave/${id}`, { method: "POST" });
        showToast(result?.Message || "✅ İzin talebi başarıyla onaylandı!", "success");
        await loadPendingLeaves();
        await loadLeaves();
        await loadEmployees();
        renderCalendarGrid();
    } catch (err) {
        showToast("Onaylama Başarısız: " + err.message, "error");
    }
}

async function rejectLeaveRecord(id) {
    if (!confirm(`#${id} numaralı izin talebini reddetmek istediğinize emin misiniz?`)) return;

    try {
        const result = await apiFetch(`${API_BASE}/reject-leave/${id}`, { method: "POST" });
        showToast(result?.Message || "🔴 İzin talebi reddedildi.", "success");
        await loadPendingLeaves();
        renderCalendarGrid();
    } catch (err) {
        showToast("Reddetme Başarısız: " + err.message, "error");
    }
}

/* ---------- Veritabanı Gezgini (İzin Geçmişi) ---------- */

async function loadLeaves() {
    setStatus("leavesStatus", "İzin kayıtları yükleniyor...", "loading");
    try {
        const records = await apiFetch(`${API_BASE}/leaves`);
        leavesCache = records || [];
        renderLeavesTable(leavesCache);
        renderLeaveKpi(leavesCache);
        setStatus("leavesStatus", "", "");
    } catch (err) {
        setStatus("leavesStatus", "İzin kayıtları alınamadı: " + err.message, "error");
    }
}

function renderLeavesTable(records) {
    const tbody = document.getElementById("leavesTableBody");
    const emptyMsg = document.getElementById("emptyLeavesMsg");
    if (!tbody) return;
    tbody.innerHTML = "";

    if (!records.length) {
        if (emptyMsg) emptyMsg.hidden = false;
        return;
    }
    if (emptyMsg) emptyMsg.hidden = true;

    const isAdmin = currentUser?.Role === "Admin";

    const visibleRecords = isAdmin
        ? records
        : records.filter(r => String(r.employeeId || (r.employee ? r.employee.id : '')) === String(currentUser?.Id));

    for (const r of visibleRecords) {
        const recId = r.id !== undefined ? r.id : r.Id;
        const leaveTypeVal = Number(r.leaveType !== undefined ? r.leaveType : r.LeaveType);
        const typeInfo = LEAVE_TYPES[leaveTypeVal] ?? { label: leaveTypeVal, badge: "" };
        const empName = r.employeeName || (r.employee ? r.employee.name : "Bilinmiyor");
        const baseSal = Number(r.baseSalary !== undefined ? r.baseSalary : (r.employee ? r.employee.monthlySalary : 0));
        const dedAmt = Number(r.deductionAmount !== undefined ? r.deductionAmount : r.CalculatedDeducation);
        const finSal = Number(r.finalSalary !== undefined ? r.finalSalary : r.FinalSalary);
        const leaveDaysVal = r.leaveDays !== undefined ? r.leaveDays : r.LeaveDays;
        const noteVal = r.note !== undefined ? r.note : r.Note;
        const startStr = formatDate(r.startDate || r.StartDate);
        const endStr = formatDate(r.endDate || r.EndDate);

        const tr = document.createElement("tr");
        tr.innerHTML = `
            <td>#${recId}</td>
            <td><strong>${escapeHtml(empName)}</strong></td>
            <td><span class="badge ${typeInfo.badge}">${escapeHtml(typeInfo.label)}</span></td>
            <td>${startStr} — ${endStr}</td>
            <td>${leaveDaysVal} Gün</td>
            <td>${formatCurrency(baseSal)}</td>
            <td class="highlight-negative">-${formatCurrency(dedAmt)}</td>
            <td class="highlight-positive"><strong>${formatCurrency(finSal)}</strong></td>
            <td>${escapeHtml(noteVal || "-")}</td>
            <td>
                <div class="row-actions">
                    ${isAdmin ? `
                    <button class="btn btn-sm btn-outline" title="Bildirim Notu Gönder" data-notify-leave="${recId}">
                        <i class="fa-solid fa-bullhorn"></i> Not Gönder
                    </button>
                    <button class="btn btn-sm btn-danger" title="İzni İptal Et" data-cancel-leave="${recId}" style="background: rgba(239, 68, 68, 0.15); color: #ef4444; border: 1px solid rgba(239, 68, 68, 0.3);">
                        <i class="fa-solid fa-trash"></i> İptal
                    </button>
                    ` : '<span class="badge badge-office">✓ Onaylandı</span>'}
                </div>
            </td>
        `;
        tbody.appendChild(tr);
    }

    tbody.querySelectorAll("[data-notify-leave]").forEach((btn) => {
        btn.addEventListener("click", () => {
            const id = btn.getAttribute("data-notify-leave");
            const record = leavesCache.find((r) => String(r.id !== undefined ? r.id : r.Id) === String(id));
            if (record) openNotificationModal(record);
        });
    });

    tbody.querySelectorAll("[data-cancel-leave]").forEach((btn) => {
        btn.addEventListener("click", () => {
            const id = btn.getAttribute("data-cancel-leave");
            cancelLeaveRecord(id);
        });
    });
}

async function cancelLeaveRecord(leaveRecordId) {
    const record = leavesCache.find((r) => String(r.id !== undefined ? r.id : r.Id) === String(leaveRecordId));
    const empName = record ? (record.employeeName || (record.employee ? record.employee.name : "Personel")) : `#${leaveRecordId}`;

    if (!confirm(`"${empName}" adlı personelin #${leaveRecordId} numaralı izin kaydını iptal etmek istediğinize emin misiniz? Maaş kesintisi kaldırılacaktır.`)) {
        return;
    }

    try {
        const result = await apiFetch(`${API_BASE}/cancel-leave/${leaveRecordId}`, {
            method: "DELETE"
        });

        showToast(result?.Message || "🗑️ İzin kaydı başarıyla iptal edildi.", "success");
        await loadLeaves();
        await loadEmployees();
        renderCalendarGrid();
    } catch (err) {
        showToast("İzin kaydı iptal edilemedi: " + err.message, "error");
    }
}

/* ---------- Bildirim Notu Modalı ---------- */

function openNotificationModal(record) {
    const form = document.getElementById("notificationForm");
    if (form) form.reset();
    setStatus("notifFormStatus", "", "");

    const recId = record.id !== undefined ? record.id : record.Id;
    const empName = record.employeeName || (record.employee ? record.employee.name : "Personel");

    const recIdEl = document.getElementById("notifLeaveRecordId");
    const nameEl = document.getElementById("notifRecipientName");

    if (recIdEl) recIdEl.value = recId;
    if (nameEl) nameEl.value = empName;

    const modal = document.getElementById("leaveNotificationModal");
    if (modal) modal.hidden = false;
}

function closeNotificationModal() {
    const modal = document.getElementById("leaveNotificationModal");
    if (modal) modal.hidden = true;
}

function initNotificationModal() {
    const closeBtn = document.getElementById("closeNotificationModal");
    const cancelBtn = document.getElementById("cancelNotificationModal");
    const modal = document.getElementById("leaveNotificationModal");
    const form = document.getElementById("notificationForm");

    if (closeBtn) closeBtn.addEventListener("click", closeNotificationModal);
    if (cancelBtn) cancelBtn.addEventListener("click", closeNotificationModal);
    if (modal) {
        modal.addEventListener("click", (e) => {
            if (e.target.id === "leaveNotificationModal") closeNotificationModal();
        });
    }

    if (form) {
        form.addEventListener("submit", async (e) => {
            e.preventDefault();

            const leaveRecordId = Number(document.getElementById("notifLeaveRecordId").value);
            const messageNote = document.getElementById("notifMessageNote").value.trim();

            const submitBtn = document.getElementById("submitNotificationBtn");
            if (submitBtn) submitBtn.disabled = true;
            setStatus("notifFormStatus", "Gönderiliyor...", "loading");

            try {
                const payload = {
                    LeaveRecordId: leaveRecordId,
                    MessageNote: messageNote
                };

                const result = await apiFetch(`${API_BASE}/send-notification`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify(payload)
                });

                showToast(result?.Message || "📢 Bildirim notu başarıyla gönderildi!", "success");
                alert(result?.Message || "📢 Bildirim notu başarıyla gönderildi!");
                closeNotificationModal();

            } catch (err) {
                setStatus("notifFormStatus", "Gönderilemedi: " + err.message, "error");
                showToast("Gönderilemedi: " + err.message, "error");
            } finally {
                if (submitBtn) submitBtn.disabled = false;
            }
        });
    }
}

/* ---------- Başlangıç ---------- */

function initApp() {
    initTheme();
    initAuth();
    initTabs();
    initEmployeeModal();
    initSimForm();
    initNotificationModal();
    initCsvUpload();
    initExportLeaves();
    initCalendar();

    const refList = document.getElementById("refreshListBtn");
    const refLeaves = document.getElementById("refreshLeavesBtn");
    const refPending = document.getElementById("refreshPendingBtn");
    const searchInp = document.getElementById("searchInput");

    if (refList) refList.addEventListener("click", loadEmployees);
    if (refLeaves) refLeaves.addEventListener("click", loadLeaves);
    if (refPending) refPending.addEventListener("click", loadPendingLeaves);
    if (searchInp) searchInp.addEventListener("input", filterEmployeeTable);

    loadEmployees();
    loadLeaves();
    loadPendingLeaves();
}

if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initApp);
} else {
    initApp();
}