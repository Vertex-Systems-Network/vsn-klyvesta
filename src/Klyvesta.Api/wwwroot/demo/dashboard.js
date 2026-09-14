(() => {
  const pkr = new Intl.NumberFormat('en-PK', {
    style: 'currency',
    currency: 'PKR',
    maximumFractionDigits: 0,
  });

  const number = new Intl.NumberFormat('en-PK');

  const changeClass = (value) => value >= 0 ? 'positive' : 'negative';
  const signedPercent = (value) => `${value >= 0 ? '+' : ''}${Number(value).toFixed(2)}%`;

  async function loadDashboard() {
    const response = await fetch('/api/demo/dashboard', { credentials: 'same-origin' });
    if (response.status === 401) {
      window.location.replace('/demo/login.html');
      return;
    }
    if (!response.ok) {
      throw new Error('Demo dashboard data could not be loaded.');
    }

    const data = await response.json();
    const account = data.account;

    document.getElementById('welcome-title').textContent = `Welcome, ${account.name}`;
    document.getElementById('account-mode').textContent = account.mode;
    document.getElementById('portfolio-value').textContent = pkr.format(account.portfolioValue);
    document.getElementById('cash-value').textContent = pkr.format(account.cash);

    const dayChange = document.getElementById('day-change');
    dayChange.textContent = `${account.dayChange >= 0 ? '+' : ''}${pkr.format(account.dayChange)}`;
    dayChange.className = changeClass(account.dayChange);

    const dayChangePercent = document.getElementById('day-change-pct');
    dayChangePercent.textContent = signedPercent(account.dayChangePercent);
    dayChangePercent.className = changeClass(account.dayChangePercent);

    document.getElementById('holdings-body').innerHTML = data.holdings.map((holding) => `
      <tr>
        <td><strong>${holding.symbol}</strong></td>
        <td>${holding.name}</td>
        <td>${number.format(holding.quantity)}</td>
        <td>${pkr.format(holding.price)}</td>
        <td>${pkr.format(holding.value)}</td>
        <td class="${changeClass(holding.changePercent)}">${signedPercent(holding.changePercent)}</td>
      </tr>`).join('');

    document.getElementById('orders-list').innerHTML = data.orders.map((order) => `
      <div class="stack-row">
        <div>
          <strong>${order.symbol} · ${order.side}</strong>
          <small>${order.type} · ${number.format(order.quantity)} shares</small>
        </div>
        <div class="right">
          <span>${pkr.format(order.price)}</span>
          <small>${order.status}</small>
        </div>
      </div>`).join('');

    document.getElementById('watchlist-list').innerHTML = data.watchlist.map((item) => `
      <div class="stack-row">
        <div><strong>${item.symbol}</strong><small>PSX synthetic quote</small></div>
        <div class="right">
          <span>${pkr.format(item.price)}</span>
          <small class="${changeClass(item.changePercent)}">${signedPercent(item.changePercent)}</small>
        </div>
      </div>`).join('');

    document.getElementById('ai-title').textContent = data.aiInsight.title;
    document.getElementById('ai-message').textContent = data.aiInsight.message;
    document.getElementById('ai-confidence').textContent = data.aiInsight.confidence;

    document.getElementById('safeguards').innerHTML = data.safeguards
      .map((item) => `<div class="safeguard-item">${item}</div>`)
      .join('');
  }

  document.getElementById('logout-button')?.addEventListener('click', async () => {
    await fetch('/api/demo/logout', { method: 'POST', credentials: 'same-origin' });
    window.location.replace('/demo/login.html');
  });

  loadDashboard().catch((error) => {
    console.error(error);
    document.getElementById('welcome-title').textContent = 'Demo unavailable';
  });
})();
