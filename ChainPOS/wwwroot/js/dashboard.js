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

function showLoadingOverlay(message, title) {
  const overlay = document.getElementById('loading-overlay');
  if (!overlay) return;

  const titleNode = overlay.querySelector('[data-loading-title]');
  const messageNode = overlay.querySelector('[data-loading-message]');
  if (titleNode) titleNode.textContent = title || 'Processing';
  if (messageNode) messageNode.textContent = message || 'Please wait while the request is processed.';
  overlay.classList.remove('hidden');
  document.body.classList.add('app-loading-active');
}

function resetLoadingControls() {
  document.querySelectorAll('[data-loading-pending="true"]').forEach(element => {
    element.classList.remove('is-loading-pending');
    element.removeAttribute('aria-busy');
    delete element.dataset.loadingPending;
  });

  document.querySelectorAll('form[data-loading-submitting="true"]').forEach(form => {
    delete form.dataset.loadingSubmitting;
  });

  document.querySelectorAll('[data-submit-guard-disabled="true"]').forEach(button => {
    button.disabled = false;
    button.classList.remove('opacity-60', 'cursor-wait');
    delete button.dataset.submitGuardDisabled;
  });

  document.querySelectorAll('form[data-get-submitting="true"]').forEach(form => {
    delete form.dataset.getSubmitting;
  });
}

function hideLoadingOverlay() {
  document.getElementById('loading-overlay')?.classList.add('hidden');
  document.body.classList.remove('app-loading-active');
  resetLoadingControls();
}

function setLoadingPending(element) {
  if (!element) return;

  element.dataset.loadingPending = 'true';
  element.setAttribute('aria-busy', 'true');
  element.classList.add('is-loading-pending');
}

function showLoadingForForm(form, submitter) {
  const title = submitter?.dataset.loadingTitle || form?.dataset.loadingTitle || 'Processing';
  const message = submitter?.dataset.loadingMessage || form?.dataset.loadingMessage || 'Please wait while the request is processed.';
  showLoadingOverlay(message, title);
}

function shouldSkipFormLoading(form) {
  if (!form) return true;
  if (form.dataset.loading === 'false' || form.closest('[data-no-loading]')) return true;
  if (form.target && form.target.toLowerCase() !== '_self') return true;
  if ((form.getAttribute('method') || '').toLowerCase() === 'dialog') return true;
  return false;
}

function isPrimaryNavigationClick(event) {
  return event.button === 0 && !event.metaKey && !event.ctrlKey && !event.shiftKey && !event.altKey;
}

function shouldSkipLinkLoading(anchor) {
  if (!anchor) return true;
  if (anchor.dataset.loading === 'false' || anchor.closest('[data-no-loading]')) return true;
  if (anchor.target && anchor.target.toLowerCase() !== '_self') return true;
  if (anchor.hasAttribute('download')) return true;
  if (anchor.hasAttribute('data-bs-toggle') || anchor.hasAttribute('data-toggle')) return true;

  const rawHref = (anchor.getAttribute('href') || '').trim();
  if (!rawHref || rawHref === '#' || rawHref.startsWith('#')) return true;

  const loweredHref = rawHref.toLowerCase();
  if (
    loweredHref.startsWith('javascript:') ||
    loweredHref.startsWith('mailto:') ||
    loweredHref.startsWith('tel:')
  ) {
    return true;
  }

  let url;
  try {
    url = new URL(anchor.href, window.location.href);
  } catch {
    return true;
  }

  if (url.origin !== window.location.origin) return true;
  if (url.pathname === window.location.pathname && url.search === window.location.search) return true;

  return false;
}

function submitConfirmedForm() {
  const modal = document.getElementById('confirm-modal');
  const formId = modal?.dataset.formId;
  if (formId) {
    const form = document.getElementById(formId);
    closeConfirmModal();
    if (form) {
      form.dataset.loadingSubmitting = 'true';
      showLoadingForForm(form);
    }
    form?.submit();
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

function initRealtimeFilters() {
  document.querySelectorAll('main form[method="get"], main form[method="GET"]').forEach(form => {
    if (form.dataset.autoFilterReady === 'true' || form.dataset.autoFilter !== 'true') return;
    const fields = Array.from(form.querySelectorAll('input, select'))
      .filter(field => {
        if (field.disabled || !field.name) return false;
        if (field.type === 'hidden' || field.type === 'submit' || field.type === 'button') return false;
        return true;
      });
    if (fields.length === 0) return;

    let timer;
    const submit = (delay = 350) => {
      window.clearTimeout(timer);
      timer = window.setTimeout(() => {
        const page = form.querySelector('input[name="Page"]');
        if (page) page.value = '1';
        form.classList.add('auto-filter-loading');
        form.requestSubmit();
      }, delay);
    };

    fields.forEach(field => {
      const eventName = field.tagName === 'SELECT' || field.type === 'date' ? 'change' : 'input';
      field.addEventListener(eventName, () => submit(eventName === 'change' ? 120 : 450));
    });

    form.addEventListener('submit', () => {
      form.classList.add('auto-filter-loading');
    });
    form.dataset.autoFilterReady = 'true';
  });
}

function initGetFormSubmitGuards() {
  document.querySelectorAll('main form[method="get"], main form[method="GET"]').forEach(form => {
    if (form.dataset.submitGuardReady === 'true') return;

    form.addEventListener('submit', event => {
      if (form.dataset.getSubmitting === 'true') {
        event.preventDefault();
        return;
      }

      form.dataset.getSubmitting = 'true';
      form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach(button => {
        if (button.disabled) return;
        button.disabled = true;
        button.dataset.submitGuardDisabled = 'true';
        button.classList.add('opacity-60', 'cursor-wait');
      });
    });

    form.dataset.submitGuardReady = 'true';
  });
}

function initLoadingOverlayTriggers() {
  document.querySelectorAll('form').forEach(form => {
    if (form.dataset.loadingReady === 'true') return;

    form.addEventListener('submit', event => {
      if (shouldSkipFormLoading(form)) return;
      if (form.dataset.loadingSubmitting === 'true') {
        event.preventDefault();
        return;
      }

      form.dataset.loadingSubmitting = 'true';
      const submitter = event.submitter;
      window.setTimeout(() => {
        if (event.defaultPrevented) {
          delete form.dataset.loadingSubmitting;
          return;
        }

        showLoadingForForm(form, submitter);
      }, 0);
    });

    form.dataset.loadingReady = 'true';
  });

  if (document.documentElement.dataset.loadingClickReady === 'true') return;

  document.addEventListener('click', event => {
    if (!isPrimaryNavigationClick(event)) return;

    const explicitTrigger = event.target.closest('[data-loading-trigger]');
    const navigationLink = event.target.closest('#app-sidebar a[href], main a[href]');
    const trigger = explicitTrigger || navigationLink;
    if (!trigger || trigger.dataset.loadingPending === 'true') {
      if (trigger?.dataset.loadingPending === 'true') event.preventDefault();
      return;
    }

    if (trigger.matches('a[href]') && shouldSkipLinkLoading(trigger)) return;
    if ('disabled' in trigger && trigger.disabled) return;
    if (trigger.getAttribute('aria-disabled') === 'true') return;

    setLoadingPending(trigger);
    window.setTimeout(() => {
      if (event.defaultPrevented) {
        trigger.classList.remove('is-loading-pending');
        trigger.removeAttribute('aria-busy');
        delete trigger.dataset.loadingPending;
        return;
      }

      showLoadingOverlay(trigger.dataset.loadingMessage, trigger.dataset.loadingTitle);
    }, 0);
  });

  document.documentElement.dataset.loadingClickReady = 'true';
}

window.showLoadingOverlay = showLoadingOverlay;
window.hideLoadingOverlay = hideLoadingOverlay;
window.addEventListener('pageshow', hideLoadingOverlay);

document.addEventListener('DOMContentLoaded', () => {
  initDataTablePagination();
  initRealtimeFilters();
  initGetFormSubmitGuards();
  initLoadingOverlayTriggers();
});
