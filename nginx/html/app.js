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

    await loadSelectedInventory();
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
    const newProductId = created.id || created._id || created.productId || 'unknown';
    const productResult = document.getElementById('product-result');
    productResult.innerText = 'Product created: ' + newProductId;

    // if user provided initial stock, set it in InventoryService via the gateway
    try {
      const stockInput = parseInt(data.stock || 0, 10);
      const prodId = created.id || created._id || created.productId;
      if (prodId && stockInput > 0) {
        await fetchJSON('/api/inventory', {
          method: 'POST',
          headers: {'Content-Type': 'application/json'},
          body: JSON.stringify({ productId: prodId, quantity: stockInput })
        });
        productResult.innerText = 'Product created and initial stock set to ' + stockInput + ' for product ' + newProductId;
        console.info('Set initial stock', prodId, stockInput);
      }
    } catch (ie) {
      document.getElementById('product-result').innerText = 'Product created, but initial stock update failed: ' + ie.message;
      console.warn('Set stock failed', ie);
    }
    ev.target.reset();
    await loadProducts();
  } catch(e) {
    alert('Create failed: ' + e.message);
  }
});

// Refresh notifications button handler
const refreshBtn = document.createElement('button');
refreshBtn.type = 'button';
refreshBtn.innerText = 'Refresh notifications';
refreshBtn.addEventListener('click', () => {
  const email = document.querySelector('input[name="email"]')?.value;
  if (email) fetchNotifications(email);
});
document.getElementById('notifications-list').before(refreshBtn);

const notificationsStatus = document.getElementById('notifications-status');
const inventoryStatus = document.getElementById('inventory-status');
let currentInventory = null;
const productSelect = document.getElementById('product-select');
productSelect.addEventListener('change', () => loadSelectedInventory());

// Create order form handler (cleaned, starts polling and shows timeline/notifications)
document.getElementById('create-order-form').addEventListener('submit', async (ev) => {
  ev.preventDefault();
  const f = Object.fromEntries(new FormData(ev.target));
  const selected = productsCache.find(p => (p.id || p._id || p.productId) === f.productId);
  const payload = {
    customerEmail: f.email,
    items: [
      {
        productId: f.productId,
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

    const orderId = order.id || order.orderId || (order.order && order.order.id) || null;
    const correlation = order.correlationId || order.correlation || null;
    document.getElementById('order-result').innerText = 'Order created: ' + (orderId || JSON.stringify(order)) + (correlation ? ' · correlationId: ' + correlation : '') + '. Waiting for final status...';

    if (f.email) {
      notificationsStatus.innerText = 'Waiting for order result...';
      fetchNotifications(f.email);
    }
    if (orderId) startOrderPoll(orderId, f.email, correlation);
    ev.target.reset();
  } catch(e) {
    document.getElementById('order-result').innerText = 'Order failed: ' + e.message;
  }
});

// timeline helper
function addTimelineEntry(text) {
  const container = document.getElementById('order-timeline');
  if (!container) return;
  if (!container.querySelector('ul')) container.innerHTML = '<ul id="timeline-list"></ul>';
  const ul = container.querySelector('ul');
  const li = document.createElement('li');
  li.innerText = new Date().toISOString() + ' — ' + text;
  ul.prepend(li);
}

// fetch notifications for email
async function fetchNotifications(email) {
  try {
    const list = document.getElementById('notifications-list');
    notificationsStatus.innerText = 'Loading notifications...';
    list.innerText = 'Loading...';
    const url = '/api/notifications/' + encodeURIComponent(email);
    const msgs = await fetchJSON(url);
    if (!msgs || msgs.length === 0) {
      list.innerText = '(no messages)';
      notificationsStatus.innerText = 'No notifications for ' + email;
      return;
    }
    list.innerHTML = msgs.map((m) => {
      try {
        const parsed = JSON.parse(m);
        return `<div class="note"><b>${parsed.Type}</b>: ${parsed.Message}<br><small>${parsed.Timestamp}</small></div>`;
      } catch {
        return `<div class="note">${m}</div>`;
      }
    }).join('');
    notificationsStatus.innerText = 'Showing ' + msgs.length + ' notification(s) for ' + email;
  } catch (e) {
    console.warn('notifications fetch failed', e);
    notificationsStatus.innerText = 'Notifications load failed';
  }
}

// BFF fetch helper — show order details JSON
document.getElementById('bff-fetch')?.addEventListener('click', async () => {
  const id = document.getElementById('bff-order-id').value?.trim();
  const out = document.getElementById('bff-result');
  if (!id) { out.innerText = 'Enter an order id'; return; }
  try {
    out.innerText = 'Loading...';
    const details = await fetchJSON('/bff/order-details/' + id);
    out.innerText = JSON.stringify(details, null, 2);
  } catch (e) {
    out.innerText = 'Failed: ' + (e.message || e);
  }
});

// Load-balancer probe: call /api/products/health multiple times and collect instance identifiers
document.getElementById('lb-probe')?.addEventListener('click', async () => {
  const out = document.getElementById('lb-results');
  out.innerText = 'Probing...';
  const counts = {};
  const attempts = 8;
  for (let i=0;i<attempts;i++){
    try{
      const res = await fetch('/api/products/health');
      // try header first
      let inst = res.headers.get('X-Instance-Id');
      if (!inst) {
        try{ const body = await res.json(); inst = body.ContainerId || body.containerId || body.Container || 'unknown'; }catch{ inst = 'unknown'; }
      }
      counts[inst] = (counts[inst]||0) + 1;
    }catch(e){ counts['error']=(counts['error']||0)+1 }
  }
  out.innerText = Object.entries(counts).map(([k,v]) => `${k}: ${v}`).join('\n');
});

async function loadSelectedInventory() {
  const selectedId = productSelect.value;
  if (!selectedId) {
    inventoryStatus.innerText = 'Inventory: no product selected';
    currentInventory = null;
    return;
  }

  try {
    const data = await fetchJSON('/api/inventory/' + selectedId);
    currentInventory = data;
    inventoryStatus.innerText = 'Inventory available: ' + data.quantity;
  } catch (e) {
    inventoryStatus.innerText = 'Inventory available: 0';
    currentInventory = { productId: selectedId, quantity: 0 };
  }
}

// poll order status until final and fetch notifications
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
          document.getElementById('order-result').innerText = 'Order ' + orderId + ' ' + statusText;
          await loadSelectedInventory();
          if (email) fetchNotifications(email);
        }
      }
    } catch (e) {
      console.debug('poll error', e.message || e);
    }
  }, 1500);
}

loadProducts();
