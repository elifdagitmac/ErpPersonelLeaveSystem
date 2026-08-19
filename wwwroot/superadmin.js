"use strict";

const SA_TOKEN_KEY = "erp-superadmin-token";
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

function escapeHtml(str) {
    const div = document.createElement("div");
    div.textContent = str ?? "";
    return div.innerHTML;
}

function formatDate(dateStr) {
    if (!dateStr) return "-";
    const d = new Date(dateStr);
    return isNaN(d.getTime()) ? "-" : d.toLocaleDateString("tr-TR");
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
    showToast._timer = setTimeout(() => toast.classList.remove("show"), 3500);
}

function setStatus(elementId, message, type) {
    const el = document.getElementById(elementId);
    if (!el) return;
    el.textContent = message || "";
    el.className = "status-msg" + (type ? " " + type : "");
}

async function saFetch(url, options) {
    const token = localStorage.getItem(SA_TOKEN_KEY);
    const finalOptions = { ...(options || {}) };
    if (token) finalOptions.headers = { ...(finalOptions.headers || {}), Authorization: `Bearer ${token}` };

    const response = await fetch(url, finalOptions);

    if (response.status === 401 && token) {
        localStorage.removeItem(SA_TOKEN_KEY);
        location.reload();
        throw new Error("Oturum süresi doldu, lütfen tekrar giriş yapın.");
    }

    if (!response.ok) {
        const rawText = await response.text();
        let detail = rawText;
        try {
            const data = JSON.parse(rawText);
            detail = data?.Message || data?.message || rawText;
        } catch { /* düz metin hata */ }
        throw new Error(detail || `İstek başarısız oldu (HTTP ${response.status}).`);
    }

    if (response.status === 204) return null;
    const text = await response.text();
    return text ? JSON.parse(text) : null;
}

function showPanel() {
    document.getElementById("saLoginModal").style.display = "none";
    document.getElementById("companiesPanel").style.display = "";
    document.getElementById("openAddCompanyModal").style.display = "inline-flex";
}

function initSaAuth() {
    const token = localStorage.getItem(SA_TOKEN_KEY);
    if (token) {
        showPanel();
        loadCompanies();
    }

    const form = document.getElementById("saLoginForm");
    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        const email = document.getElementById("saEmail").value.trim();
        const password = document.getElementById("saPassword").value;

        try {
            const response = await saFetch("/api/auth/superadmin-login", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ Email: email, Password: password })
            });

            localStorage.setItem(SA_TOKEN_KEY, response.token);
            showPanel();
            showToast("🔑 Süper Admin girişi başarılı.", "success");
            await loadCompanies();
        } catch (err) {
            setStatus("saLoginStatus", err.message, "error");
        }
    });
}

async function loadCompanies() {
    try {
        const companies = await saFetch("/api/superadmin/companies");
        renderCompaniesTable(companies || []);
    } catch (err) {
        showToast("Şirket listesi alınamadı: " + err.message, "error");
    }
}

function renderCompaniesTable(companies) {
    const tbody = document.getElementById("companiesTableBody");
    const emptyMsg = document.getElementById("emptyCompaniesMsg");
    tbody.innerHTML = "";

    if (!companies.length) {
        emptyMsg.hidden = false;
        return;
    }
    emptyMsg.hidden = true;

    for (const c of companies) {
        const tr = document.createElement("tr");
        tr.innerHTML = `
            <td>${c.id}</td>
            <td><strong>${escapeHtml(c.companyCode)}</strong></td>
            <td>${escapeHtml(c.companyName)}</td>
            <td>${escapeHtml(c.adminEmail)}</td>
            <td><span class="badge ${c.isActive ? 'badge-office' : 'badge-noentry'}">${c.isActive ? '🟢 Aktif' : '🔴 Pasif'}</span></td>
            <td>${formatDate(c.createdAt)}</td>
            <td style="display:flex; gap:6px;">
                <button class="btn btn-sm btn-secondary" data-toggle="${c.id}" title="Lisans durumunu değiştir">
                    <i class="fa-solid fa-power-off"></i>
                </button>
                <button class="btn btn-sm btn-secondary" style="color: var(--danger);" data-delete="${c.id}" title="Şirketi sil">
                    <i class="fa-solid fa-trash"></i>
                </button>
            </td>
        `;
        tbody.appendChild(tr);
    }

    tbody.querySelectorAll("[data-toggle]").forEach(btn => {
        btn.addEventListener("click", () => toggleCompany(btn.getAttribute("data-toggle")));
    });
    tbody.querySelectorAll("[data-delete]").forEach(btn => {
        btn.addEventListener("click", () => deleteCompany(btn.getAttribute("data-delete")));
    });
}

async function toggleCompany(id) {
    try {
        await saFetch(`/api/superadmin/companies/${id}/toggle-active`, { method: "POST" });
        showToast("Lisans durumu güncellendi.", "success");
        await loadCompanies();
    } catch (err) {
        showToast("İşlem başarısız: " + err.message, "error");
    }
}

async function deleteCompany(id) {
    if (!confirm("Bu şirketi ve TÜM verilerini (personel, izin, duyuru, avans kayıtları) kalıcı olarak silmek istediğinize emin misiniz?")) return;
    try {
        await saFetch(`/api/superadmin/companies/${id}`, { method: "DELETE" });
        showToast("Şirket silindi.", "success");
        await loadCompanies();
    } catch (err) {
        showToast("Silme başarısız: " + err.message, "error");
    }
}

function initCompanyModal() {
    const modal = document.getElementById("companyModal");
    const openBtn = document.getElementById("openAddCompanyModal");
    const closeBtn = document.getElementById("closeCompanyModal");
    const cancelBtn = document.getElementById("cancelCompanyModal");
    const form = document.getElementById("companyForm");

    const open = () => { form.reset(); setStatus("companyFormStatus", "", ""); modal.hidden = false; };
    const close = () => { modal.hidden = true; };

    openBtn.addEventListener("click", open);
    closeBtn.addEventListener("click", close);
    cancelBtn.addEventListener("click", close);
    modal.addEventListener("click", (e) => { if (e.target.id === "companyModal") close(); });

    form.addEventListener("submit", async (e) => {
        e.preventDefault();
        const payload = {
            CompanyCode: document.getElementById("coCode").value.trim(),
            CompanyName: document.getElementById("coName").value.trim(),
            AdminEmail: document.getElementById("coAdminEmail").value.trim(),
            AdminName: document.getElementById("coAdminName").value.trim() || "Şirket Yöneticisi",
            MasterPassword: document.getElementById("coMasterPassword").value
        };

        setStatus("companyFormStatus", "Kaydediliyor...", "loading");
        try {
            await saFetch("/api/superadmin/companies", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });
            showToast("🏢 Şirket başarıyla oluşturuldu.", "success");
            close();
            await loadCompanies();
        } catch (err) {
            setStatus("companyFormStatus", err.message, "error");
        }
    });
}

document.addEventListener("DOMContentLoaded", () => {
    initTheme();
    initSaAuth();
    initCompanyModal();
});
