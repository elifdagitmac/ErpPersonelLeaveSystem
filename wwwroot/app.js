"use strict";

const API_BASE = "/api/personnel";

const LEAVE_TYPES = {
    1: { label: "Yıllık İzin", badge: "badge-yillik" },
    2: { label: "Ücretsiz İzin", badge: "badge-ucretsiz" },
    3: { label: "Sağlık İzni", badge: "badge-saglik" },
    4: { label: "Mazeret İzni", badge: "badge-mazaret" },
};

const WORK_STATUSES = {
    1: { label: "Şirkette / Ofiste", emoji: "🟢", badge: "badge-office" },
    2: { label: "Remote / Evden", emoji: "🔵", badge: "badge-remote" },
    3: { label: "İzinli", emoji: "🟡", badge: "badge-onleave" },
    4: { label: "Giriş Yapılmadı", emoji: "🔴", badge: "badge-noentry" },
};

let employeesCache = [];
let lastCalculatedResult = null;

/* ---------- Tema (Açık / Koyu) ---------- */

const THEME_STORAGE_KEY = "erp-theme";

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

    document.getElementById("themeToggle").addEventListener("click", () => {
        const isLight = document.documentElement.getAttribute("data-theme") === "light";
        const next = isLight ? "dark" : "light";
        applyTheme(next);
        localStorage.setItem(THEME_STORAGE_KEY, next);
    });
}

/* ---------- Yardımcı Fonksiyonlar ---------- */

function formatCurrency(value) {
    const number = Number(value);
    return number.toLocaleString("tr-TR", { style: "currency", currency: "TRY" });
}

function escapeHtml(str) {
    const div = document.createElement("div");
    div.textContent = str ?? "";
    return div.innerHTML;
}

function showToast(message, type) {
    const toast = document.getElementById("toast");
    toast.textContent = message;
    toast.className = "toast show" + (type ? " " + type : "");
    clearTimeout(showToast._timer);
    showToast._timer = setTimeout(() => {
        toast.classList.remove("show");
    }, 3500);
}

function setStatus(elementId, message, type) {
    const el = document.getElementById(elementId);
    el.textContent = message || "";
    el.className = "status-msg" + (type ? " " + type : "");
}

async function apiFetch(url, options) {
    const response = await fetch(url, options);
    if (!response.ok) {
        let detail = "";
        try {
            const data = await response.json();
            detail = data?.title || data?.message || data?.Message || JSON.stringify(data);
        } catch {
            detail = await response.text();
        }
        throw new Error(detail || `İstek başarısız oldu (HTTP ${response.status}).`);
    }
    if (response.status === 204) return null;
    const text = await response.text();
    return text ? JSON.parse(text) : null;
}

/* ---------- Sekme Geçişleri ---------- */

function initTabs() {
    const buttons = document.querySelectorAll(".tab-btn");
    buttons.forEach((btn) => {
        btn.addEventListener("click", () => {
            buttons.forEach((b) => b.classList.remove("active"));
            document.querySelectorAll(".tab-panel").forEach((p) => p.classList.remove("active"));

            btn.classList.add("active");
            document.getElementById(btn.dataset.tab).classList.add("active");

            if (btn.dataset.tab === "tab-explorer") {
                loadLeaves();
            }
        });
    });
}

/* ---------- KPI Kartları ---------- */

function renderKpis() {
    const totalEmployees = employeesCache.length;
    const inOffice = employeesCache.filter((e) => Number(e.workStatus) === 1).length;
    const totalPayroll = employeesCache.reduce((sum, e) => sum + Number(e.monthlySalary || 0), 0);

    document.getElementById("kpiTotalEmployees").textContent = totalEmployees;
    document.getElementById("kpiInOffice").textContent = inOffice;
    document.getElementById("kpiTotalPayroll").textContent = formatCurrency(totalPayroll);
}

function renderLeaveKpi(leaveRecords) {
    const totalDays = leaveRecords.reduce((sum, r) => sum + Number(r.leaveDays || 0), 0);
    document.getElementById("kpiTotalLeaveDays").textContent = totalDays;
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
    tbody.innerHTML = "";

    if (!employees.length) {
        emptyMsg.hidden = false;
        return;
    }
    emptyMsg.hidden = true;

    for (const emp of employees) {
        const statusInfo = WORK_STATUSES[Number(emp.workStatus)] ?? { label: "Bilinmiyor", emoji: "⚪", badge: "" };

        const tr = document.createElement("tr");
        tr.innerHTML = `
            <td>${emp.id}</td>
            <td>${escapeHtml(emp.name)}</td>
            <td>${escapeHtml(emp.department)}</td>
            <td><span class="badge ${statusInfo.badge}">${statusInfo.emoji} ${escapeHtml(statusInfo.label)}</span></td>
            <td>${formatCurrency(emp.monthlySalary)}</td>
            <td>
                <div class="row-actions">
                    <button class="btn-icon" title="Düzenle" data-edit-emp="${emp.id}"><i class="fa-solid fa-pen"></i></button>
                    <button class="btn-icon danger" title="Sil" data-delete-emp="${emp.id}"><i class="fa-solid fa-trash"></i></button>
                    <button class="btn-icon" title="İzin Simülasyonu" data-select-emp="${emp.id}"><i class="fa-solid fa-bolt"></i></button>
                </div>
            </td>
        `;
        tbody.appendChild(tr);
    }

    tbody.querySelectorAll("[data-select-emp]").forEach((btn) => {
        btn.addEventListener("click", () => {
            const id = btn.getAttribute("data-select-emp");
            document.getElementById("simEmployee").value = id;
            document.getElementById("simEmployee").scrollIntoView({ behavior: "smooth", block: "center" });
        });
    });

    tbody.querySelectorAll("[data-edit-emp]").forEach((btn) => {
        btn.addEventListener("click", () => {
            const id = btn.getAttribute("data-edit-emp");
            const employee = employeesCache.find((e) => String(e.id) === String(id));
            if (employee) openEmployeeModal(employee);
        });
    });

    tbody.querySelectorAll("[data-delete-emp]").forEach((btn) => {
        btn.addEventListener("click", () => deleteEmployee(btn.getAttribute("data-delete-emp")));
    });
}

function filterEmployeeTable() {
    const term = document.getElementById("searchInput").value.trim().toLowerCase();
    if (!term) {
        renderEmployeeTable(employeesCache);
        return;
    }
    const filtered = employeesCache.filter(
        (e) =>
            e.name?.toLowerCase().includes(term) ||
            e.department?.toLowerCase().includes(term)
    );
    renderEmployeeTable(filtered);
}

async function deleteEmployee(id) {
    const employee = employeesCache.find((e) => String(e.id) === String(id));
    const name = employee ? employee.name : `#${id}`;
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
    form.reset();
    setStatus("employeeFormStatus", "", "");

    const isEdit = Boolean(employee);
    document.getElementById("empId").value = isEdit ? employee.id : "";
    document.getElementById("employeeModalTitle").innerHTML = isEdit
        ? '<i class="fa-solid fa-pen"></i> Personel Düzenle'
        : '<i class="fa-solid fa-user-plus"></i> Yeni Personel Ekle';
    document.getElementById("submitEmployeeBtn").innerHTML = isEdit
        ? '<i class="fa-solid fa-floppy-disk"></i> Güncelle'
        : '<i class="fa-solid fa-floppy-disk"></i> Kaydet';

    // Backend PUT ucu yalnızca ad, departman, maaş, deneyim, yaş ve PDKS durumunu günceller;
    // eğitim/cinsiyet bu yüzden düzenleme sırasında salt-okunur bırakılıyor.
    document.getElementById("empEducationField").style.display = isEdit ? "none" : "";
    document.getElementById("empGenderField").style.display = isEdit ? "none" : "";
    document.getElementById("empEducation").required = !isEdit;
    document.getElementById("empGender").required = !isEdit;
    document.getElementById("empEditNote").hidden = !isEdit;

    if (isEdit) {
        document.getElementById("empName").value = employee.name ?? "";
        document.getElementById("empDepartment").value = employee.department ?? "";
        document.getElementById("empExperience").value = employee.experienceYears ?? 0;
        document.getElementById("empAge").value = employee.age ?? "";
        document.getElementById("empSalary").value = employee.monthlySalary ?? "";
        document.getElementById("empWorkStatus").value = employee.workStatus ?? 1;
    } else {
        document.getElementById("empWorkStatus").value = 1;
    }

    document.getElementById("employeeModal").hidden = false;
}

function closeEmployeeModal() {
    document.getElementById("employeeModal").hidden = true;
}

function initEmployeeModal() {
    document.getElementById("openAddEmployeeModal").addEventListener("click", () => openEmployeeModal(null));
    document.getElementById("closeEmployeeModal").addEventListener("click", closeEmployeeModal);
    document.getElementById("cancelEmployeeModal").addEventListener("click", closeEmployeeModal);
    document.getElementById("employeeModal").addEventListener("click", (e) => {
        if (e.target.id === "employeeModal") closeEmployeeModal();
    });

    document.getElementById("employeeForm").addEventListener("submit", async (e) => {
        e.preventDefault();

        const id = document.getElementById("empId").value;
        const isEdit = Boolean(id);

        const payload = {
            Name: document.getElementById("empName").value.trim(),
            Department: document.getElementById("empDepartment").value.trim(),
            ExperienceYears: Number(document.getElementById("empExperience").value),
            EducationLevel: isEdit ? "" : document.getElementById("empEducation").value,
            Age: Number(document.getElementById("empAge").value),
            Gender: isEdit ? "" : document.getElementById("empGender").value,
            MonthlySalary: Number(document.getElementById("empSalary").value),
            WorkStatus: Number(document.getElementById("empWorkStatus").value),
        };

        const submitBtn = document.getElementById("submitEmployeeBtn");
        submitBtn.disabled = true;
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
            showToast("İşlem başarısız oldu.", "error");
        } finally {
            submitBtn.disabled = false;
        }
    });
}

/* ---------- İzin & Maaş Simülatörü ---------- */

function populateSimEmployeeSelect(employees) {
    const select = document.getElementById("simEmployee");
    const currentValue = select.value;
    select.innerHTML = '<option value="" disabled selected>Personel seçiniz</option>';

    for (const emp of employees) {
        const opt = document.createElement("option");
        opt.value = emp.id;
        opt.textContent = `${emp.name} — ${emp.department} (${formatCurrency(emp.monthlySalary)})`;
        select.appendChild(opt);
    }

    if (currentValue) select.value = currentValue;
}

function initSimForm() {
    const form = document.getElementById("simForm");
    const confirmBtn = document.getElementById("confirmLeaveBtn");

    form.addEventListener("submit", async (e) => {
        e.preventDefault();

        const employeeId = document.getElementById("simEmployee").value;
        const leaveType = Number(document.getElementById("simLeaveType").value);
        const leaveDays = Number(document.getElementById("simLeaveDays").value);

        if (!employeeId) {
            setStatus("simStatus", "Lütfen bir personel seçin.", "error");
            return;
        }

        const employee = employeesCache.find((e) => String(e.id) === String(employeeId));
        if (!employee) {
            setStatus("simStatus", "Seçilen personel bulunamadı, listeyi yenileyin.", "error");
            return;
        }

        setStatus("simStatus", "Hesaplanıyor...", "loading");
        confirmBtn.disabled = true;

        try {
            const query = new URLSearchParams({
                monthlySalary: employee.monthlySalary,
                leaveType: leaveType,
                leaveDays: leaveDays,
            });

            const result = await apiFetch(`${API_BASE}/calculate?${query.toString()}`, {
                method: "POST",
            });

            lastCalculatedResult = { ...result, employeeId, leaveType, leaveDays };
            renderResult(result, leaveType, leaveDays);
            setStatus("simStatus", "", "");
            confirmBtn.disabled = false;
        } catch (err) {
            setStatus("simStatus", "Hesaplama başarısız: " + err.message, "error");
            document.getElementById("resultCard").hidden = true;
        }
    });

    confirmBtn.addEventListener("click", async () => {
        if (!lastCalculatedResult) return;

        const employeeId = document.getElementById("simEmployee").value;
        const leaveType = Number(document.getElementById("simLeaveType").value);
        const leaveDays = Number(document.getElementById("simLeaveDays").value);
        const note = document.getElementById("simNote").value.trim();

        confirmBtn.disabled = true;
        setStatus("simStatus", "İzin kaydı oluşturuluyor...", "loading");

        try {
            const payload = {
                employeeId: Number(employeeId),
                LeaveType: leaveType,
                LeaveDays: leaveDays,
                Note: note || null,
            };

            await apiFetch(`${API_BASE}/add-leave`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload),
            });

            setStatus("simStatus", "İzin kaydı başarıyla oluşturuldu.", "success");
            showToast("İzin kaydı başarıyla oluşturuldu.", "success");

            document.getElementById("simNote").value = "";
            document.getElementById("resultCard").hidden = true;
            lastCalculatedResult = null;

            await loadLeaves();
        } catch (err) {
            setStatus("simStatus", "İzin kaydı oluşturulamadı: " + err.message, "error");
            showToast("İzin kaydı oluşturulamadı.", "error");
            confirmBtn.disabled = false;
        }
    });
}

function renderResult(result, leaveType, leaveDays) {
    const card = document.getElementById("resultCard");
    card.hidden = false;

    document.getElementById("resBaseSalary").textContent = formatCurrency(result.baseMonthlySalary);
    document.getElementById("resDailyWage").textContent = formatCurrency(result.dailyWage);
    document.getElementById("resLeaveType").textContent = LEAVE_TYPES[leaveType]?.label ?? leaveType;
    document.getElementById("resLeaveDays").textContent = leaveDays;
    document.getElementById("resDeduction").textContent = formatCurrency(result.deductionAmount);
    document.getElementById("resFinalSalary").textContent = formatCurrency(result.finalNetSalary);
    document.getElementById("resFormula").textContent = result.formulaApplied || "";
}

/* ---------- Veritabanı Gezgini (İzin Geçmişi) ---------- */

async function loadLeaves() {
    setStatus("leavesStatus", "İzin kayıtları yükleniyor...", "loading");
    try {
        const records = await apiFetch(`${API_BASE}/leaves`);
        renderLeavesTable(records || []);
        renderLeaveKpi(records || []);
        setStatus("leavesStatus", "", "");
    } catch (err) {
        setStatus("leavesStatus", "İzin kayıtları alınamadı: " + err.message, "error");
    }
}

function renderLeavesTable(records) {
    const tbody = document.getElementById("leavesTableBody");
    const emptyMsg = document.getElementById("emptyLeavesMsg");
    tbody.innerHTML = "";

    if (!records.length) {
        emptyMsg.hidden = false;
        return;
    }
    emptyMsg.hidden = true;

    for (const r of records) {
        const typeInfo = LEAVE_TYPES[Number(r.leaveType)] ?? { label: r.leaveType, badge: "" };

        const tr = document.createElement("tr");
        tr.innerHTML = `
            <td>${r.id}</td>
            <td>${escapeHtml(r.employeeName)}</td>
            <td><span class="badge ${typeInfo.badge}">${escapeHtml(typeInfo.label)}</span></td>
            <td>${r.leaveDays}</td>
            <td>${formatCurrency(r.baseSalary)}</td>
            <td class="highlight-negative">-${formatCurrency(r.deductionAmount)}</td>
            <td class="highlight-positive">${formatCurrency(r.finalSalary)}</td>
            <td>${escapeHtml(r.note || "-")}</td>
        `;
        tbody.appendChild(tr);
    }
}

/* ---------- Başlangıç ---------- */

document.addEventListener("DOMContentLoaded", () => {
    initTheme();
    initTabs();
    initEmployeeModal();
    initSimForm();

    document.getElementById("refreshListBtn").addEventListener("click", loadEmployees);
    document.getElementById("refreshLeavesBtn").addEventListener("click", loadLeaves);
    document.getElementById("searchInput").addEventListener("input", filterEmployeeTable);

    loadEmployees();
    loadLeaves();
});
