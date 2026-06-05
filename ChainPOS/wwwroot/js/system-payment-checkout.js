(() => {
  const shell = document.querySelector("[data-system-payment-shell]");
  const copyButtons = document.querySelectorAll("[data-copy-target]");
  const modal = document.querySelector("[data-system-payment-modal]");
  const modalIcon = document.querySelector("[data-payment-modal-icon]");
  const modalTitle = document.querySelector("[data-payment-modal-title]");
  const modalMessage = document.querySelector("[data-payment-modal-message]");
  const modalClose = document.querySelector("[data-payment-modal-close]");

  const getStorageKey = () => {
    if (!(shell instanceof HTMLElement)) {
      return "";
    }

    const paymentId = shell.dataset.paymentId;
    return paymentId ? `chainpos:system-payment:${paymentId}:notice` : "";
  };

  const setQueuedNotice = (noticeType) => {
    const storageKey = getStorageKey();
    if (!storageKey) {
      return;
    }

    try {
      window.sessionStorage.setItem(storageKey, noticeType);
    } catch {
      // The page still reloads and shows the final status.
    }
  };

  const consumeQueuedNotice = () => {
    const storageKey = getStorageKey();
    if (!storageKey) {
      return null;
    }

    try {
      const noticeType = window.sessionStorage.getItem(storageKey);
      window.sessionStorage.removeItem(storageKey);
      return noticeType;
    } catch {
      return null;
    }
  };

  const showStatusModal = (noticeType) => {
    if (!(modal instanceof HTMLElement)) {
      return;
    }

    const isExpired = noticeType === "expired";
    if (modalIcon) {
      modalIcon.textContent = isExpired ? "!" : "OK";
      modalIcon.className = isExpired
        ? "flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-red-50 text-xl font-bold text-red-600"
        : "flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-green-50 text-xl font-bold text-green-600";
    }

    if (modalTitle) {
      modalTitle.textContent = isExpired ? "QR expired" : "Payment completed";
    }

    if (modalMessage) {
      modalMessage.textContent = isExpired
        ? "This SePay QR code has expired. Please contact the platform admin if you need a new payment request."
        : "ChainPOS received the SePay webhook and marked this payment as paid.";
    }

    modal.classList.remove("hidden");
    modal.classList.add("flex");
    modal.setAttribute("aria-hidden", "false");
    modalClose?.focus();
  };

  const hideStatusModal = () => {
    if (!(modal instanceof HTMLElement)) {
      return;
    }

    modal.classList.add("hidden");
    modal.classList.remove("flex");
    modal.setAttribute("aria-hidden", "true");
  };

  copyButtons.forEach((button) => {
    button.addEventListener("click", async () => {
      const targetId = button.getAttribute("data-copy-target");
      const target = targetId ? document.getElementById(targetId) : null;
      const value = (target?.textContent || "").trim();
      if (!value) {
        return;
      }

      try {
        await navigator.clipboard.writeText(value);
        const previousText = button.textContent;
        button.textContent = "Copied";
        window.setTimeout(() => {
          button.textContent = previousText || "Copy";
        }, 1500);
      } catch {
        button.textContent = "Copy failed";
      }
    });
  });

  modalClose?.addEventListener("click", hideStatusModal);
  modal?.addEventListener("click", (event) => {
    if (event.target === modal) {
      hideStatusModal();
    }
  });

  if (!(shell instanceof HTMLElement)) {
    return;
  }

  const queuedNotice = consumeQueuedNotice();
  if (queuedNotice === "paid" || queuedNotice === "expired") {
    showStatusModal(queuedNotice);
  }

  const pollUrl = shell.dataset.pollUrl;
  const isFinal = shell.dataset.isFinal === "true";
  if (!pollUrl || isFinal) {
    return;
  }

  const poll = async () => {
    try {
      const response = await fetch(pollUrl, {
        method: "GET",
        credentials: "same-origin",
        headers: {
          Accept: "application/json",
        },
      });

      if (!response.ok) {
        return;
      }

      const status = await response.json();
      if (status?.isPaid || status?.isExpired) {
        setQueuedNotice(status.isPaid ? "paid" : "expired");
        window.location.reload();
      }
    } catch {
      // Keep polling on the next tick.
    }
  };

  window.setInterval(poll, 5000);
})();
