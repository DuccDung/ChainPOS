function toggleSidebar() {
  document.getElementById('app-sidebar')?.classList.toggle('open');
}

function openConfirmModal(formId, title, message) {
  const modal = document.getElementById('confirm-modal');
  if (!modal) return true;

  modal.dataset.formId = formId;
  modal.querySelector('[data-confirm-title]').textContent = title || 'Confirm action';
  modal.querySelector('[data-confirm-message]').textContent = message || 'Are you sure?';
  modal.classList.remove('hidden');
  return false;
}

function closeConfirmModal() {
  document.getElementById('confirm-modal')?.classList.add('hidden');
}

function submitConfirmedForm() {
  const modal = document.getElementById('confirm-modal');
  const formId = modal?.dataset.formId;
  if (formId) {
    document.getElementById(formId)?.submit();
  }
}

function initDataTablePagination() {
  document.querySelectorAll('table.data-table').forEach(table => {
    if (table.dataset.paginationReady === 'true') return;
    const tbody = table.querySelector('tbody');
    if (!tbody) return;

    const pageSize = Number(table.dataset.pageSize || 10);
    const getRows = () => Array.from(tbody.querySelectorAll(':scope > tr'))
      .filter(row => !row.querySelector('td[colspan]'));
    let page = 1;
    let controls;

    const render = () => {
      const rows = getRows();
      if (rows.length <= pageSize) {
        rows.forEach(row => row.classList.remove('hidden'));
        controls?.remove();
        controls = null;
        return;
      }

      const totalPages = Math.max(1, Math.ceil(rows.length / pageSize));
      page = Math.min(page, totalPages);
      rows.forEach((row, index) => {
        const rowPage = Math.floor(index / pageSize) + 1;
        row.classList.toggle('hidden', rowPage !== page);
      });

      if (!controls) {
        controls = document.createElement('div');
        controls.className = 'flex items-center justify-between gap-3 px-5 py-3 border-t border-gray-50 text-xs text-gray-500';
        table.closest('.overflow-x-auto')?.after(controls);
      }

      controls.innerHTML = `
        <span>Page ${page} of ${totalPages} · ${rows.length} records</span>
        <div class="flex items-center gap-2">
          <button type="button" data-page-prev class="px-3 py-1.5 rounded-lg border border-gray-200 font-semibold ${page <= 1 ? 'opacity-40 cursor-not-allowed' : 'hover:bg-gray-50'}" ${page <= 1 ? 'disabled' : ''}>Prev</button>
          <button type="button" data-page-next class="px-3 py-1.5 rounded-lg border border-gray-200 font-semibold ${page >= totalPages ? 'opacity-40 cursor-not-allowed' : 'hover:bg-gray-50'}" ${page >= totalPages ? 'disabled' : ''}>Next</button>
        </div>`;
      controls.querySelector('[data-page-prev]')?.addEventListener('click', () => {
        page -= 1;
        render();
      });
      controls.querySelector('[data-page-next]')?.addEventListener('click', () => {
        page += 1;
        render();
      });
    };

    new MutationObserver(() => render()).observe(tbody, { childList: true });
    table.dataset.paginationReady = 'true';
    render();
  });
}

document.addEventListener('DOMContentLoaded', initDataTablePagination);
