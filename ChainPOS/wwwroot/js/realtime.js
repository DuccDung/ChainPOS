(() => {
  if (!window.signalR) {
    console.warn('SignalR client was not loaded; realtime updates are disabled.');
    return;
  }

  const money = new Intl.NumberFormat('vi-VN');
  const notifications = [];
  let unread = 0;
  let dropdown;
  let toastRoot;

  const read = (payload, camelName, pascalName) => payload?.[camelName] ?? payload?.[pascalName];
  const normalizeId = value => (value || '').toString().toLowerCase();
  const formatNumber = value => money.format(Number(value || 0));
  const formatDateTime = value => {
    if (!value) return '-';
    const date = new Date(value);
    return Number.isNaN(date.getTime())
      ? '-'
      : date.toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' });
  };
  const escapeHtml = value => (value ?? '').toString()
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
  const parseMetricNumber = text => Number((text || '').replace(/[^\d.-]/g, '')) || 0;

  function currentLivePage() {
    return document.querySelector('[data-live-page]')?.dataset.livePage || '';
  }

  function currentStoreId() {
    return normalizeId(document.querySelector('[data-live-store-id]')?.dataset.liveStoreId);
  }

  function ensureToastRoot() {
    if (toastRoot) return toastRoot;
    toastRoot = document.createElement('div');
    toastRoot.id = 'live-toast-root';
    toastRoot.className = 'live-toast-root';
    document.body.appendChild(toastRoot);
    return toastRoot;
  }

  function ensureDropdown() {
    if (dropdown) return dropdown;
    const button = document.getElementById('live-notification-button');
    if (!button) return null;

    dropdown = document.createElement('div');
    dropdown.id = 'live-notification-dropdown';
    dropdown.className = 'live-notification-dropdown hidden';
    dropdown.innerHTML = `
      <div class="live-notification-header">
        <span>Live updates</span>
        <button type="button" data-live-clear>Clear</button>
      </div>
      <div class="live-notification-list" data-live-list>
        <div class="live-empty">No live updates yet.</div>
      </div>`;
    button.parentElement.appendChild(dropdown);

    button.addEventListener('click', () => {
      dropdown.classList.toggle('hidden');
      unread = 0;
      renderBadge();
    });
    dropdown.querySelector('[data-live-clear]')?.addEventListener('click', () => {
      notifications.length = 0;
      renderNotifications();
      unread = 0;
      renderBadge();
    });
    document.addEventListener('click', event => {
      if (!dropdown || dropdown.classList.contains('hidden')) return;
      if (dropdown.contains(event.target) || button.contains(event.target)) return;
      dropdown.classList.add('hidden');
    });
    return dropdown;
  }

  function renderBadge() {
    const badge = document.getElementById('live-notification-badge');
    if (!badge) return;
    badge.textContent = unread > 9 ? '9+' : unread.toString();
    badge.classList.toggle('hidden', unread === 0);
  }

  function renderNotifications() {
    const panel = ensureDropdown();
    const list = panel?.querySelector('[data-live-list]');
    if (!list) return;
    if (notifications.length === 0) {
      list.innerHTML = '<div class="live-empty">No live updates yet.</div>';
      return;
    }

    list.innerHTML = notifications
      .slice(0, 12)
      .map(item => `
        <div class="live-notification-item">
          <p>${item.title}</p>
          <span>${item.message}</span>
          <small>${item.time}</small>
        </div>`)
      .join('');
  }

  function pushNotification(title, message) {
    notifications.unshift({
      title,
      message,
      time: new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })
    });
    unread += 1;
    renderBadge();
    renderNotifications();
  }

  function showToast(title, message, kind = 'info') {
    pushNotification(title, message);

    const root = ensureToastRoot();
    const toast = document.createElement('div');
    toast.className = `live-toast live-toast-${kind}`;
    const titleNode = document.createElement('p');
    titleNode.textContent = title;
    const messageNode = document.createElement('span');
    messageNode.textContent = message;
    toast.append(titleNode, messageNode);
    root.appendChild(toast);

    window.setTimeout(() => toast.classList.add('live-toast-exit'), 4500);
    window.setTimeout(() => toast.remove(), 5200);
  }

  function showReloadBanner(message) {
    const pageRoot = document.querySelector('[data-live-page]');
    if (!pageRoot) return;

    let banner = pageRoot.querySelector('[data-live-reload-banner]');
    if (!banner) {
      banner = document.createElement('div');
      banner.dataset.liveReloadBanner = 'true';
      banner.className = 'live-reload-banner';
      const text = document.createElement('span');
      text.dataset.liveReloadText = 'true';
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = 'Reload';
      button.addEventListener('click', () => window.location.reload());
      banner.append(text, button);
      pageRoot.prepend(banner);
    }

    banner.querySelector('[data-live-reload-text]').textContent = message;
  }

  function highlight(element) {
    if (!element) return;
    element.classList.remove('live-highlight');
    window.requestAnimationFrame(() => {
      element.classList.add('live-highlight');
      window.setTimeout(() => element.classList.remove('live-highlight'), 1800);
    });
  }

  function setStockBadge(badge, quantity, minQuantity) {
    if (!badge) return;
    badge.className = 'inline-flex rounded-full border px-2.5 py-0.5 text-xs font-semibold';
    if (quantity <= 0) {
      badge.classList.add('border-red-100', 'bg-red-50', 'text-red-700');
      badge.textContent = 'Out of Stock';
    } else if (quantity <= minQuantity) {
      badge.classList.add('border-amber-100', 'bg-amber-50', 'text-amber-700');
      badge.textContent = 'Low Stock';
    } else {
      badge.classList.add('border-green-100', 'bg-green-50', 'text-green-700');
      badge.textContent = 'In Stock';
    }
  }

  function updateInventoryRows(payload) {
    const productId = normalizeId(read(payload, 'productId', 'ProductId'));
    const storeId = normalizeId(read(payload, 'storeId', 'StoreId'));
    const quantity = Number(read(payload, 'quantity', 'Quantity') || 0);
    const minQuantity = Number(read(payload, 'minQuantity', 'MinQuantity') || 0);
    let updated = false;

    document.querySelectorAll('[data-realtime-inventory-row]').forEach(row => {
      if (normalizeId(row.dataset.productId) !== productId || normalizeId(row.dataset.storeId) !== storeId) return;
      row.dataset.minQuantity = minQuantity.toString();
      const quantityCell = row.querySelector('[data-realtime-quantity]');
      const minCell = row.querySelector('[data-realtime-min-quantity]');
      if (quantityCell) quantityCell.textContent = formatNumber(quantity);
      if (minCell) minCell.textContent = formatNumber(minQuantity);
      setStockBadge(row.querySelector('[data-realtime-stock-status]'), quantity, minQuantity);
      highlight(row);
      updated = true;
    });

    return updated;
  }

  function updatePosCards(payload) {
    const productId = normalizeId(read(payload, 'productId', 'ProductId'));
    const storeId = normalizeId(read(payload, 'storeId', 'StoreId'));
    const pageStoreId = currentStoreId();
    if (pageStoreId && pageStoreId !== storeId) return false;

    const quantity = Number(read(payload, 'quantity', 'Quantity') || 0);
    let updated = false;
    document.querySelectorAll('.product-card').forEach(card => {
      if (normalizeId(card.dataset.productId) !== productId) return;
      card.dataset.stock = quantity.toString();
      const stockText = card.querySelector('[data-realtime-pos-stock]');
      if (stockText) {
        stockText.textContent = `Stock ${formatNumber(quantity)}`;
        stockText.classList.toggle('text-amber-600', quantity > 0 && quantity <= Number(read(payload, 'minQuantity', 'MinQuantity') || 0));
        stockText.classList.toggle('text-gray-400', quantity <= 0 || quantity > Number(read(payload, 'minQuantity', 'MinQuantity') || 0));
      }
      card.disabled = quantity <= 0;
      card.classList.toggle('opacity-60', quantity <= 0);
      card.classList.toggle('bg-gray-50', quantity <= 0);
      highlight(card);
      updated = true;
    });
    return updated;
  }

  function badgeClasses(status, type) {
    const base = type === 'order'
      ? 'px-2.5 py-0.5 rounded-full text-xs font-semibold'
      : 'badge-delivered px-2.5 py-0.5 rounded-full text-xs font-semibold';
    if (status === 'Cancelled' || status === 'Failed') return `badge-cancelled ${base}`;
    if (status === 'Completed' || status === 'Paid' || status === 'Closed') return `badge-delivered ${base}`;
    if (status === 'Open') return `badge-processing ${base}`;
    return `badge-pending ${base}`;
  }

  function updateDashboardMetric(key, updater) {
    const valueNode = document.querySelector(`[data-dashboard-metric="${key}"] [data-dashboard-metric-value]`);
    if (!valueNode) return false;
    const next = updater(valueNode.textContent || '0');
    valueNode.textContent = next;
    highlight(valueNode.closest('[data-dashboard-metric]'));
    return true;
  }

  function incrementDashboardMetric(key, delta = 1) {
    return updateDashboardMetric(key, text => formatNumber(parseMetricNumber(text) + delta));
  }

  function addDashboardCurrency(key, delta) {
    return updateDashboardMetric(key, text => {
      const prefix = text.includes('$') ? '$' : '';
      return `${prefix}${formatNumber(parseMetricNumber(text) + Number(delta || 0))}`;
    });
  }

  function prependOrderRow(payload) {
    const table = document.querySelector('[data-live-orders-table]');
    const tbody = table?.querySelector('tbody');
    if (!tbody) return false;

    const storeId = normalizeId(read(payload, 'storeId', 'StoreId'));
    const pageStoreId = currentStoreId();
    if (pageStoreId && pageStoreId !== storeId) return false;

    const orderId = read(payload, 'orderId', 'OrderId');
    if (!orderId || tbody.querySelector(`[data-order-id="${orderId}"]`)) return false;

    const area = window.location.pathname.toLowerCase().startsWith('/staff') ? 'staff' : 'owner';
    const orderStatus = read(payload, 'orderStatus', 'OrderStatus') || 'Completed';
    const paymentStatus = read(payload, 'paymentStatus', 'PaymentStatus') || 'Paid';
    const row = document.createElement('tr');
    row.className = 'hover:bg-orange-50/30';
    row.dataset.realtimeOrderRow = 'true';
    row.dataset.orderId = orderId;
    row.dataset.storeId = storeId;
    row.innerHTML = `
      <td class="px-5 py-3.5 font-mono text-sm text-orange-500 font-semibold">${escapeHtml(read(payload, 'orderCode', 'OrderCode'))}</td>
      <td class="px-5 py-3.5"><p class="font-semibold text-gray-800">${escapeHtml(read(payload, 'storeName', 'StoreName'))}</p><p class="font-mono text-xs text-gray-400">${escapeHtml(read(payload, 'storeCode', 'StoreCode'))}</p></td>
      <td class="px-5 py-3.5 text-gray-600">${escapeHtml(read(payload, 'staffName', 'StaffName') || '-')}</td>
      <td class="px-5 py-3.5 text-center font-bold text-gray-900">${formatNumber(read(payload, 'itemCount', 'ItemCount'))}</td>
      <td class="px-5 py-3.5 text-right font-bold text-gray-900">${formatNumber(read(payload, 'totalAmount', 'TotalAmount'))}</td>
      <td class="px-5 py-3.5"><span data-realtime-payment-status class="${badgeClasses(paymentStatus, 'payment')}">${escapeHtml(paymentStatus)}</span></td>
      <td class="px-5 py-3.5"><span data-realtime-order-status class="${badgeClasses(orderStatus, 'order')}">${escapeHtml(orderStatus)}</span></td>
      <td class="px-5 py-3.5 text-gray-400 text-xs">${formatDateTime(read(payload, 'createdAt', 'CreatedAt'))}</td>
      <td class="px-5 py-3.5"><a href="/${area}/orders/details/${orderId}" class="px-2.5 py-1 bg-orange-50 text-orange-600 text-xs font-semibold rounded-lg hover:bg-orange-100">View</a></td>`;

    const empty = tbody.querySelector('td[colspan]');
    if (empty) empty.closest('tr')?.remove();
    tbody.prepend(row);
    highlight(row);
    return true;
  }

  function prependDashboardOrder(payload) {
    const tbody = document.querySelector('[data-dashboard-recent-orders]');
    if (!tbody) return false;
    const orderId = read(payload, 'orderId', 'OrderId');
    if (!orderId || tbody.querySelector(`[data-order-id="${orderId}"]`)) return false;
    const orderStatus = read(payload, 'orderStatus', 'OrderStatus') || 'Completed';
    const row = document.createElement('tr');
    row.className = 'hover:bg-orange-50/30';
    row.dataset.realtimeOrderRow = 'true';
    row.dataset.orderId = orderId;
    row.innerHTML = `
      <td class="px-5 py-3.5 font-mono text-sm text-orange-500 font-semibold">${escapeHtml(read(payload, 'orderCode', 'OrderCode'))}</td>
      <td class="px-5 py-3.5"><p class="font-semibold text-gray-800">${escapeHtml(read(payload, 'storeName', 'StoreName'))}</p><p class="font-mono text-xs text-gray-400">${escapeHtml(read(payload, 'storeCode', 'StoreCode'))}</p></td>
      <td class="px-5 py-3.5 text-gray-600">${escapeHtml(read(payload, 'staffName', 'StaffName') || '-')}</td>
      <td class="px-5 py-3.5 text-right font-bold text-gray-900">${formatNumber(read(payload, 'totalAmount', 'TotalAmount'))}</td>
      <td class="px-5 py-3.5"><span data-realtime-order-status class="${badgeClasses(orderStatus, 'order')}">${escapeHtml(orderStatus)}</span></td>
      <td class="px-5 py-3.5 text-gray-400 text-xs">${formatDateTime(read(payload, 'createdAt', 'CreatedAt'))}</td>`;
    tbody.prepend(row);
    while (tbody.querySelectorAll('tr').length > 6) tbody.lastElementChild?.remove();
    highlight(row);
    return true;
  }

  function prependPaymentRow(payload) {
    const table = document.querySelector('[data-live-payments-table]');
    const tbody = table?.querySelector('tbody');
    if (!tbody) return false;
    const paymentId = read(payload, 'paymentId', 'PaymentId');
    if (!paymentId || tbody.querySelector(`[data-payment-id="${paymentId}"]`)) return false;
    const status = read(payload, 'status', 'Status') || 'Pending';
    const row = document.createElement('tr');
    row.className = 'hover:bg-orange-50/30';
    row.dataset.realtimePaymentRow = 'true';
    row.dataset.paymentId = paymentId;
    row.innerHTML = `
      <td class="px-5 py-3.5 font-semibold text-gray-800">${escapeHtml(read(payload, 'tenantName', 'TenantName'))}</td>
      <td class="px-5 py-3.5 text-gray-600">${escapeHtml(read(payload, 'planName', 'PlanName'))}</td>
      <td class="px-5 py-3.5 text-right font-bold text-gray-900">${formatNumber(read(payload, 'amount', 'Amount'))}</td>
      <td class="px-5 py-3.5 text-gray-600">${escapeHtml(read(payload, 'method', 'Method'))}</td>
      <td class="px-5 py-3.5"><span data-realtime-payment-status class="${badgeClasses(status, 'payment')}">${escapeHtml(status)}</span></td>
      <td class="px-5 py-3.5 text-xs text-gray-400">${formatDateTime(read(payload, 'occurredAt', 'OccurredAt'))}</td>
      <td class="px-5 py-3.5 text-xs text-gray-400">${formatDateTime(read(payload, 'paidAt', 'PaidAt'))}</td>
      <td class="px-5 py-3.5"><span class="text-xs text-gray-300">-</span></td>
      <td class="px-5 py-3.5"><span class="text-xs text-gray-300">Reload for actions</span></td>`;
    const empty = tbody.querySelector('td[colspan]');
    if (empty) empty.closest('tr')?.remove();
    tbody.prepend(row);
    highlight(row);
    return true;
  }

  function handleInventoryChanged(payload) {
    const productName = read(payload, 'productName', 'ProductName') || 'Product';
    const changeType = read(payload, 'changeType', 'ChangeType') || 'Inventory';
    const quantity = read(payload, 'quantity', 'Quantity');
    const rowUpdated = updateInventoryRows(payload);
    const cardUpdated = updatePosCards(payload);

    showToast('Inventory updated', `${changeType}: ${productName} now has ${formatNumber(quantity)} in stock.`, 'inventory');
    const page = currentLivePage();
    if ((page === 'inventory' && !rowUpdated) || (page === 'pos' && !cardUpdated)) {
      showReloadBanner('Inventory changed in another session. Reload to show new rows or filters.');
    }
    if (page === 'dashboard') {
      showReloadBanner('Inventory changed. Reload if you need exact low stock counts after this movement.');
    }
  }

  function handleOrderCreated(payload) {
    const orderCode = read(payload, 'orderCode', 'OrderCode') || 'New order';
    const total = read(payload, 'totalAmount', 'TotalAmount');
    showToast('Order created', `${orderCode} completed for ${formatNumber(total)}.`, 'order');
    if (currentLivePage() === 'orders') {
      if (!prependOrderRow(payload)) {
        showReloadBanner(`${orderCode} was created in another session. Reload to show it in the list.`);
      }
    }
    if (currentLivePage() === 'dashboard') {
      incrementDashboardMetric('orders-today');
      addDashboardCurrency('revenue-today', total);
      prependDashboardOrder(payload);
    }
  }

  function handleOrderCancelled(payload) {
    const orderId = normalizeId(read(payload, 'orderId', 'OrderId'));
    const orderCode = read(payload, 'orderCode', 'OrderCode') || 'Order';
    const paymentStatus = read(payload, 'paymentStatus', 'PaymentStatus') || 'Cancelled';
    const orderStatus = read(payload, 'orderStatus', 'OrderStatus') || 'Cancelled';
    let updated = false;

    document.querySelectorAll('[data-realtime-order-row]').forEach(row => {
      if (normalizeId(row.dataset.orderId) !== orderId) return;
      const orderBadge = row.querySelector('[data-realtime-order-status]');
      const paymentBadge = row.querySelector('[data-realtime-payment-status]');
      if (orderBadge) {
        orderBadge.textContent = orderStatus;
        orderBadge.className = badgeClasses(orderStatus, 'order');
      }
      if (paymentBadge) {
        paymentBadge.textContent = paymentStatus;
        paymentBadge.className = badgeClasses(paymentStatus, 'payment');
      }
      highlight(row);
      updated = true;
    });

    showToast('Order cancelled', `${orderCode} was cancelled and inventory was returned.`, 'order');
    if (currentLivePage() === 'orders' && !updated) {
      showReloadBanner(`${orderCode} was cancelled in another session. Reload to refresh the current list.`);
    }
    if (currentLivePage() === 'dashboard') {
      showReloadBanner(`${orderCode} was cancelled. Reload to refresh revenue and recent order status.`);
    }
  }

  function handleShiftChanged(payload) {
    const shiftId = normalizeId(read(payload, 'shiftId', 'ShiftId'));
    const status = read(payload, 'status', 'Status') || 'Updated';
    const storeName = read(payload, 'storeName', 'StoreName') || 'Store';
    let updated = false;

    document.querySelectorAll('[data-realtime-shift-row]').forEach(row => {
      if (normalizeId(row.dataset.shiftId) !== shiftId) return;
      const statusBadge = row.querySelector('[data-realtime-shift-status]');
      if (statusBadge) {
        statusBadge.textContent = status;
        statusBadge.className = badgeClasses(status, 'shift');
      }
      const expected = row.querySelector('[data-realtime-shift-expected]');
      const difference = row.querySelector('[data-realtime-shift-difference]');
      const closed = row.querySelector('[data-realtime-shift-closed]');
      const expectedCash = read(payload, 'expectedCash', 'ExpectedCash');
      const differenceAmount = read(payload, 'differenceAmount', 'DifferenceAmount');
      if (expected) expected.textContent = expectedCash == null ? '-' : formatNumber(expectedCash);
      if (difference) difference.textContent = differenceAmount == null ? '-' : formatNumber(differenceAmount);
      if (closed && status === 'Closed') closed.textContent = formatDateTime(read(payload, 'occurredAt', 'OccurredAt'));
      highlight(row);
      updated = true;
    });

    showToast('Shift updated', `${storeName} shift is now ${status}.`, 'shift');
    if ((currentLivePage() === 'shifts' || currentLivePage() === 'pos') && !updated) {
      showReloadBanner(`A shift changed at ${storeName}. Reload to refresh available actions.`);
    }
  }

  function handleSubscriptionChanged(payload) {
    const tenantName = read(payload, 'tenantName', 'TenantName') || 'Tenant';
    const planName = read(payload, 'planName', 'PlanName') || 'plan';
    showToast('Subscription changed', `${tenantName} is now on ${planName}.`, 'billing');
    if (['subscription', 'plans', 'payments'].includes(currentLivePage())) {
      showReloadBanner('Subscription data changed in another session. Reload to refresh billing data.');
    }
  }

  function handleSystemPaymentChanged(payload) {
    const tenantName = read(payload, 'tenantName', 'TenantName') || 'Tenant';
    const status = read(payload, 'status', 'Status') || 'Updated';
    const amount = read(payload, 'amount', 'Amount');
    showToast('System payment updated', `${tenantName}: ${formatNumber(amount)} is ${status}.`, 'billing');
    if (['subscription', 'payments'].includes(currentLivePage())) {
      if (currentLivePage() !== 'payments' || !prependPaymentRow(payload)) {
        showReloadBanner('Payment data changed in another session. Reload to refresh billing data.');
      }
    }
    if (currentLivePage() === 'dashboard' && status === 'Paid') {
      addDashboardCurrency('saas-revenue', amount);
    }
  }

  ensureDropdown();

  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/chainpos')
    .withAutomaticReconnect()
    .build();

  connection.on('InventoryChanged', handleInventoryChanged);
  connection.on('OrderCreated', handleOrderCreated);
  connection.on('OrderCancelled', handleOrderCancelled);
  connection.on('ShiftChanged', handleShiftChanged);
  connection.on('SubscriptionChanged', handleSubscriptionChanged);
  connection.on('SystemPaymentChanged', handleSystemPaymentChanged);
  connection.onreconnecting(() => document.body.dataset.liveStatus = 'reconnecting');
  connection.onreconnected(() => document.body.dataset.liveStatus = 'connected');
  connection.onclose(() => document.body.dataset.liveStatus = 'disconnected');

  connection
    .start()
    .then(() => {
      document.body.dataset.liveStatus = 'connected';
    })
    .catch(error => {
      document.body.dataset.liveStatus = 'disconnected';
      console.warn('Realtime connection failed.', error);
    });
})();
