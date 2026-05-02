// UtilityMenu Site — site.js
// Client-side utilities for Blazor interop

'use strict';

// ── Clipboard ─────────────────────────────────────────────────
window.copyToClipboard = async function (text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        // Fallback for older browsers / non-HTTPS
        try {
            const el = document.createElement('textarea');
            el.value = text;
            el.style.position = 'fixed';
            el.style.opacity = '0';
            document.body.appendChild(el);
            el.focus();
            el.select();
            const ok = document.execCommand('copy');
            document.body.removeChild(el);
            return ok;
        } catch {
            return false;
        }
    }
};

// ── Scroll Utilities ─────────────────────────────────────────
window.scrollToTop = function () {
    window.scrollTo({ top: 0, behavior: 'smooth' });
};

window.scrollToElement = function (elementId) {
    const el = document.getElementById(elementId);
    if (el) {
        el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
};

// ── Bootstrap Tooltip / Popover init ─────────────────────────
function initBootstrapComponents() {
    // Tooltips
    const tooltipEls = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    tooltipEls.forEach(el => {
        if (!el._bsTooltip) {
            el._bsTooltip = new bootstrap.Tooltip(el);
        }
    });
    // Popovers
    const popoverEls = document.querySelectorAll('[data-bs-toggle="popover"]');
    popoverEls.forEach(el => {
        if (!el._bsPopover) {
            el._bsPopover = new bootstrap.Popover(el);
        }
    });
}

// Re-init after each Blazor enhanced navigation
document.addEventListener('DOMContentLoaded', initBootstrapComponents);
document.addEventListener('blazor:navigated', initBootstrapComponents);

// ── Sidebar Toggle (mobile) ───────────────────────────────────
window.toggleSidebar = function (sidebarId) {
    const sidebar = document.getElementById(sidebarId);
    if (sidebar) sidebar.classList.toggle('open');
};

window.closeSidebar = function (sidebarId) {
    const sidebar = document.getElementById(sidebarId);
    if (sidebar) sidebar.classList.remove('open');
};

// ── Focus Management ──────────────────────────────────────────
window.focusElement = function (selector) {
    const el = document.querySelector(selector);
    if (el) el.focus();
};

// ── Local Storage helpers (for Blazor interop) ───────────────
window.localStorageHelper = {
    getItem: function (key) {
        return localStorage.getItem(key);
    },
    setItem: function (key, value) {
        localStorage.setItem(key, value);
    },
    removeItem: function (key) {
        localStorage.removeItem(key);
    }
};

// ── Page Title ────────────────────────────────────────────────
window.setDocumentTitle = function (title) {
    document.title = title;
};

// ── File Download Trigger ─────────────────────────────────────
// Used by Download.razor to initiate a tracked download via the API endpoint.
// Uses fetch() + blob URL so the current page is never navigated away — even
// on error — which avoids the back-button retry loop.
window.triggerFileDownload = async function (url) {
    try {
        const response = await fetch(url, { credentials: 'include' });
        if (!response.ok) return false;

        const blob = await response.blob();
        const blobUrl = URL.createObjectURL(blob);

        const a = document.createElement('a');
        a.href = blobUrl;

        // Extract filename from Content-Disposition if present
        const disposition = response.headers.get('Content-Disposition') || '';
        const match = disposition.match(/filename="?([^";\n]+)"?/);
        a.download = match ? match[1].trim() : 'UtilityMenu-Setup.exe';

        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);

        // Revoke the blob URL after the browser has had time to start the download
        setTimeout(() => URL.revokeObjectURL(blobUrl), 30000);
        return true;
    } catch {
        return false;
    }
};
