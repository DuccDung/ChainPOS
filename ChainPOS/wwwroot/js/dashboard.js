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
