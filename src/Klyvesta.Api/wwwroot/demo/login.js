(() => {
  const form = document.getElementById('login-form');
  const button = document.getElementById('login-button');
  const error = document.getElementById('login-error');

  form?.addEventListener('submit', async (event) => {
    event.preventDefault();
    error.hidden = true;
    button.disabled = true;
    button.textContent = 'Opening demo…';

    const payload = {
      email: document.getElementById('email').value,
      password: document.getElementById('password').value,
    };

    try {
      const response = await fetch('/api/demo/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'same-origin',
        body: JSON.stringify(payload),
      });

      if (!response.ok) {
        throw new Error('Invalid demo credentials. Use the values shown on this page.');
      }

      const result = await response.json();
      window.location.assign(result.redirect || '/demo/dashboard.html');
    } catch (err) {
      error.textContent = err instanceof Error ? err.message : 'Unable to enter demo mode.';
      error.hidden = false;
      button.disabled = false;
      button.textContent = 'Enter demo dashboard';
    }
  });
})();
