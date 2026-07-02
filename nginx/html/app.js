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
    document.getElementById('order-result').innerText = 'Order created: ' + (order.id || order.orderId || JSON.stringify(order));
    ev.target.reset();
  } catch(e) {
    document.getElementById('order-result').innerText = 'Order failed: ' + e.message;
  }
});

loadProducts();
