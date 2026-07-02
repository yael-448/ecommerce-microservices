async function fetchJSON(path, opts) {
  const res = await fetch(path, opts);
  if (!res.ok) throw new Error(res.status + ' ' + res.statusText);
  return res.json();
}

async function loadProducts() {
  const list = document.getElementById('product-list');
  try {
    const products = await fetchJSON('/api/products');
    if (!products || products.length === 0) list.innerText = 'No products';
    else {
      list.innerHTML = products.map(p=>`<div class="product"><b>${p.name}</b> — $${p.price} <small>(id: ${p.id || p._id || p.productId})</small></div>`).join('');
    }
  } catch(e) {
    list.innerText = 'Error loading products: ' + e.message;
  }
}

document.getElementById('create-product-form').addEventListener('submit', async (ev)=>{
  ev.preventDefault();
  const data = Object.fromEntries(new FormData(ev.target));
  data.price = parseFloat(data.price);
  try{
    const created = await fetchJSON('/api/products',{ method: 'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify(data)});
    alert('Created product id: ' + (created.id || created._id || created.productId || 'unknown'));
    ev.target.reset();
    await loadProducts();
  } catch(e){ alert('Create failed: ' + e.message); }
});

document.getElementById('create-order-form').addEventListener('submit', async (ev)=>{
  ev.preventDefault();
  const f = Object.fromEntries(new FormData(ev.target));
  const payload = { customerEmail: f.email, items: [{ productId: f.productId, productName: '', quantity: parseInt(f.qty,10), unitPrice: 0 }] };
  try{
    const order = await fetchJSON('/api/orders',{ method: 'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify(payload)});
    document.getElementById('order-result').innerText = 'Order created: ' + (order.id || order.orderId || JSON.stringify(order));
    ev.target.reset();
  } catch(e){ document.getElementById('order-result').innerText = 'Order failed: ' + e.message; }
});

loadProducts();
