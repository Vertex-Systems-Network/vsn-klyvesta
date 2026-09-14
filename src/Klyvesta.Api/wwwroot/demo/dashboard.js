(() => {
  const pkr = new Intl.NumberFormat('en-PK', {
    style: 'currency',
    currency: 'PKR',
    maximumFractionDigits: 0,
  });
  const number = new Intl.NumberFormat('en-PK');
  let currentDashboard = null;

  const changeClass = (value) => Number(value) >= 0 ? 'positive' : 'negative';
  const signedPercent = (value) => `${Number(value) >= 0 ? '+' : ''}${Number(value).toFixed(2)}%`;
  const escapeHtml = (value) => String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');

  async function api(url, options = {}) {
    const request = {
      credentials: 'same-origin',
      ...options,
      headers: {
        ...(options.body ? { 'Content-Type': 'application/json', 'X-Demo-Request': '1' } : {}),
        ...(options.headers || {}),
      },
    };
    const response = await fetch(url, request);
    if (response.status === 401) {
      window.location.replace('/demo/login.html');
      throw new Error('Demo session expired.');
    }
    if (!response.ok) {
      let reason = 'Demo request failed.';
      try {
        const payload = await response.json();
        reason = payload.error || reason;
      } catch {
        // Keep the generic safe error message.
      }
      throw new Error(reason);
    }
    return response.status === 204 ? null : response.json();
  }

  function renderSummary(data) {
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

    document.getElementById('risk-band').textContent = data.riskProfile.riskBand;
    document.getElementById('risk-score').textContent = `Score ${data.riskProfile.score}/100 · demo indicator`;
    document.getElementById('risk-band-large').textContent = data.riskProfile.riskBand;
    document.getElementById('risk-score-large').textContent = `Score ${data.riskProfile.score}/100`;
  }

  function renderProfile(data) {
    const profile = data.profile;
    document.getElementById('profile-display-name').value = profile.displayName;
    document.getElementById('profile-experience').value = profile.experienceLevel;
    document.getElementById('profile-horizon').value = profile.investmentHorizon;
    document.getElementById('profile-goal').value = profile.primaryGoal;
    document.getElementById('profile-monthly').value = profile.monthlyContribution;

    const answers = data.riskProfile.answers;
    document.getElementById('risk-loss').value = answers.lossTolerance;
    document.getElementById('risk-experience').value = answers.marketExperience;
    document.getElementById('risk-horizon').value = answers.horizonCapacity;
    document.getElementById('risk-liquidity').value = answers.liquidityNeed;
  }

  function renderPortfolio(data) {
    document.getElementById('holdings-body').innerHTML = data.holdings.map((holding) => `
      <tr>
        <td><strong>${escapeHtml(holding.symbol)}</strong></td>
        <td>${escapeHtml(holding.name)}</td>
        <td>${number.format(holding.quantity)}</td>
        <td>${pkr.format(holding.price)}</td>
        <td>${pkr.format(holding.value)}</td>
        <td class="${changeClass(holding.changePercent)}">${signedPercent(holding.changePercent)}</td>
      </tr>`).join('');

    document.getElementById('orders-list').innerHTML = data.orders.map((order) => `
      <div class="stack-row">
        <div>
          <strong>${escapeHtml(order.symbol)} · ${escapeHtml(order.side)}</strong>
          <small>${escapeHtml(order.type)} · ${number.format(order.quantity)} shares</small>
        </div>
        <div class="right">
          <span>${pkr.format(order.price)}</span>
          <small>${escapeHtml(order.status)}</small>
        </div>
      </div>`).join('');
  }

  function renderGoals(data) {
    const list = document.getElementById('goals-list');
    list.innerHTML = data.goals.length ? data.goals.map((goal) => {
      const progress = goal.targetAmount > 0 ? Math.min(100, goal.currentAmount / goal.targetAmount * 100) : 0;
      return `
        <div class="goal-card">
          <div class="goal-copy">
            <strong>${escapeHtml(goal.name)}</strong>
            <small>${pkr.format(goal.currentAmount)} of ${pkr.format(goal.targetAmount)} · ${escapeHtml(goal.targetDate)}</small>
            <div class="progress-track"><span style="width:${progress.toFixed(1)}%"></span></div>
          </div>
          <button class="text-button danger-text" type="button" data-goal-delete="${escapeHtml(goal.id)}">Remove</button>
        </div>`;
    }).join('') : '<p class="empty-state">No demo goals yet.</p>';
  }

  function renderWatchlist(data) {
    document.getElementById('watchlist-list').innerHTML = data.watchlist.length ? data.watchlist.map((item) => `
      <div class="stack-row">
        <div><strong>${escapeHtml(item.symbol)}</strong><small>PSX synthetic quote</small></div>
        <div class="right watchlist-actions">
          <span>${pkr.format(item.price)}</span>
          <small class="${changeClass(item.changePercent)}">${signedPercent(item.changePercent)}</small>
          <button class="text-button danger-text" type="button" data-watchlist-delete="${escapeHtml(item.symbol)}">Remove</button>
        </div>
      </div>`).join('') : '<p class="empty-state">Watchlist is empty.</p>';

    const existing = new Set(data.watchlist.map((item) => item.symbol));
    const select = document.getElementById('watchlist-symbol');
    select.innerHTML = data.availableWatchlistSymbols
      .filter((symbol) => !existing.has(symbol))
      .map((symbol) => `<option value="${escapeHtml(symbol)}">${escapeHtml(symbol)}</option>`)
      .join('');
    document.querySelector('#watchlist-form button').disabled = !select.options.length;
  }

  function renderActivity(data) {
    document.getElementById('activity-list').innerHTML = data.activity.length ? data.activity.map((event) => `
      <div class="stack-row timeline-row">
        <div>
          <strong>${escapeHtml(event.message)}</strong>
          <small>${escapeHtml(event.code)}</small>
        </div>
        <div class="right"><small>${new Date(event.occurredAt).toLocaleString()}</small></div>
      </div>`).join('') : '<p class="empty-state">No activity yet.</p>';
  }

  function renderInsight(data) {
    document.getElementById('ai-title').textContent = data.aiInsight.title;
    document.getElementById('ai-message').textContent = data.aiInsight.message;
    document.getElementById('ai-confidence').textContent = data.aiInsight.confidence;
    document.getElementById('safeguards').innerHTML = data.safeguards
      .map((item) => `<div class="safeguard-item">${escapeHtml(item)}</div>`)
      .join('');
  }

  async function loadDashboard() {
    const data = await api('/api/demo/dashboard');
    currentDashboard = data;
    renderSummary(data);
    renderProfile(data);
    renderPortfolio(data);
    renderGoals(data);
    renderWatchlist(data);
    renderActivity(data);
    renderInsight(data);
  }

  document.getElementById('profile-form')?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const status = document.getElementById('profile-save-status');
    status.textContent = 'Saving…';
    try {
      await api('/api/demo/profile', {
        method: 'PUT',
        body: JSON.stringify({
          displayName: document.getElementById('profile-display-name').value,
          experienceLevel: document.getElementById('profile-experience').value,
          investmentHorizon: document.getElementById('profile-horizon').value,
          primaryGoal: document.getElementById('profile-goal').value,
          monthlyContribution: Number(document.getElementById('profile-monthly').value),
        }),
      });
      status.textContent = 'Saved';
      await loadDashboard();
    } catch (error) {
      status.textContent = error.message;
    }
  });

  document.getElementById('risk-form')?.addEventListener('submit', async (event) => {
    event.preventDefault();
    await api('/api/demo/risk-profile', {
      method: 'PUT',
      body: JSON.stringify({
        lossTolerance: Number(document.getElementById('risk-loss').value),
        marketExperience: Number(document.getElementById('risk-experience').value),
        horizonCapacity: Number(document.getElementById('risk-horizon').value),
        liquidityNeed: Number(document.getElementById('risk-liquidity').value),
      }),
    });
    await loadDashboard();
  });

  document.getElementById('goal-form')?.addEventListener('submit', async (event) => {
    event.preventDefault();
    const targetAmount = Number(document.getElementById('goal-target').value);
    const currentAmount = Number(document.getElementById('goal-current').value);
    if (currentAmount > targetAmount) {
      window.alert('Current amount cannot exceed the target in this demo.');
      return;
    }
    await api('/api/demo/goals', {
      method: 'POST',
      body: JSON.stringify({
        name: document.getElementById('goal-name').value,
        targetAmount,
        currentAmount,
        targetDate: document.getElementById('goal-date').value,
      }),
    });
    event.currentTarget.reset();
    document.getElementById('goal-current').value = '0';
    await loadDashboard();
  });

  document.getElementById('goals-list')?.addEventListener('click', async (event) => {
    const button = event.target.closest('[data-goal-delete]');
    if (!button) return;
    await api(`/api/demo/goals/${encodeURIComponent(button.dataset.goalDelete)}`, {
      method: 'DELETE',
      body: JSON.stringify({ action: 'remove' }),
    });
    await loadDashboard();
  });

  document.getElementById('watchlist-form')?.addEventListener('submit', async (event) => {
    event.preventDefault();
    await api('/api/demo/watchlist', {
      method: 'POST',
      body: JSON.stringify({ symbol: document.getElementById('watchlist-symbol').value }),
    });
    await loadDashboard();
  });

  document.getElementById('watchlist-list')?.addEventListener('click', async (event) => {
    const button = event.target.closest('[data-watchlist-delete]');
    if (!button) return;
    await api(`/api/demo/watchlist/${encodeURIComponent(button.dataset.watchlistDelete)}`, {
      method: 'DELETE',
      body: JSON.stringify({ action: 'remove' }),
    });
    await loadDashboard();
  });

  document.getElementById('reset-demo-button')?.addEventListener('click', async () => {
    if (!window.confirm('Reset the local synthetic investor profile, goals and watchlist?')) return;
    await api('/api/demo/reset', { method: 'POST', body: JSON.stringify({ action: 'reset' }) });
    await loadDashboard();
  });

  document.getElementById('logout-button')?.addEventListener('click', async () => {
    await fetch('/api/demo/logout', { method: 'POST', credentials: 'same-origin' });
    window.location.replace('/demo/login.html');
  });

  loadDashboard().catch((error) => {
    console.error(error);
    document.getElementById('welcome-title').textContent = 'Demo unavailable';
    document.getElementById('account-mode').textContent = error.message;
  });
})();
