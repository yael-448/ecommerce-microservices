async function fetchJSON(path, opts) {
  const res = await fetch(path, opts);
  if (!res.ok) throw new Error(res.status + ' ' + res.statusText);
  return res.json();
}

let productsCache = [];

async function loadProducts() {
  const list = document.getElementById('product-list');
  const select = document.getElementById('product-select');
  try {
    const products = await fetchJSON('/api/products');
    productsCache = products;
    if (!products || products.length === 0) {
      list.innerText = 'No products';
      select.innerHTML = '<option value="">No product available</option>';
      return;
    }

    list.innerHTML = products.map(p => `
      <div class="product">
        <b>${p.name}</b> — $${p.price}
        <div class="product-meta">id: ${p.id || p._id || p.productId} · category: ${p.category || 'n/a'}</div>
      </div>
    `).join('');

    select.innerHTML = products.map(p => {
      const id = p.id || p._id || p.productId;
      return `<option value="${id}">${p.name} — $${p.price} (${id})</option>`;
    }).join('');
  } catch(e) {
    list.innerText = 'Error loading products: ' + e.message;
    document.getElementById('product-select').innerHTML = '<option value="">Error loading</option>';
  }
}

document.getElementById('create-product-form').addEventListener('submit', async (ev) => {
  ev.preventDefault();
  const data = Object.fromEntries(new FormData(ev.target));
  data.price = parseFloat(data.price);
  data.description = data.description || 'No description';
  try {
    const created = await fetchJSON('/api/products', {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify(data)
    });
    alert('Created product id: ' + (created.id || created._id || created.productId || 'unknown'));
    ev.target.reset();
    await loadProducts();
  } catch(e) {
    alert('Create failed: ' + e.message);
  }
});

document.getElementById('create-order-form').addEventListener('submit', async (ev) => {
  ev.preventDefault();
    const orderId = order.id || order.orderId || (order.order && order.order.id) || null;
    const correlation = order.correlationId || order.correlation || null;
    document.getElementById('order-result').innerText = 'Order created: ' + (orderId || JSON.stringify(order)) + (correlation ? ' · correlationId: ' + correlation : '');
    // start polling order status and show timeline + notifications
    if (orderId) startOrderPoll(orderId, f.email, correlation);
  const selected = productsCache.find(p => (p.id || p._id || p.productId) === f.productId);
  const payload = {
    customerEmail: f.email,
    items: [
      {
        productId: f.productId,

function addTimelineEntry(text) {
  const container = document.getElementById('order-timeline');
  if (!container) return;
  if (!container.querySelector('ul')) container.innerHTML = '<ul id="timeline-list"></ul>';
  const ul = container.querySelector('ul');
  const li = document.createElement('li');
  li.innerText = new Date().toISOString() + ' — ' + text;
  ul.prepend(li);
}

async function fetchNotifications(email) {
  try {
    const list = document.getElementById('notifications-list');
    list.innerText = 'Loading...';
    const url = '/api/notifications/' + encodeURIComponent(email);
    const msgs = await fetchJSON(url);
    if (!msgs || msgs.length === 0) {
      list.innerText = '(no messages)';
      return;
    }
    list.innerHTML = msgs.map(m => `<div class="note">${m}</div>`).join('');
  } catch (e) {
    console.warn('notifications fetch failed', e);
  }
}

async function startOrderPoll(orderId, email, correlationId) {
  addTimelineEntry('Order placed (id: ' + orderId + ')');
  if (correlationId) addTimelineEntry('Correlation: ' + correlationId);
  let lastStatus = null;
  const iv = setInterval(async () => {
    try {
      const details = await fetchJSON('/bff/order-details/' + orderId);
      const status = details.status !== undefined ? details.status : (details.Status ?? null);
      let statusText = String(status);
      if (status === 0 || status === '0') statusText = 'Pending';
      else if (status === 1 || status === '1') statusText = 'Confirmed';
      else if (status === 2 || status === '2') statusText = 'Rejected';
      else if (status === 3 || status === '3') statusText = 'Cancelled';
      if (status !== null && status !== lastStatus) {
        lastStatus = status;
        addTimelineEntry('Status: ' + statusText);
        if (status !== 0 && status !== '0') {
          clearInterval(iv);
          // fetch notifications for this email
          if (email) fetchNotifications(email);
        }
      }
    } catch (e) {
      // ignore transient errors while polling
      console.debug('poll error', e.message || e);
    }
  }, 1500);
}
        productName: selected?.name || 'Unknown product',
        quantity: parseInt(f.qty, 10),
        unitPrice: selected?.price || 0
      }
    ]
  };
  try {
    const order = await fetchJSON('/api/orders', {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify(payload)
    });
    document.getElementById('order-result').innerText = 'Order created: ' + (order.id || order.orderId || JSON.stringify(order));
    ev.target.reset();
  } catch(e) {
    document.getElementById('order-result').innerText = 'Order failed: ' + e.message;
  }
});

loadProducts();
