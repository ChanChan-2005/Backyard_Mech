(() => {
    const root = document.querySelector('[data-parts-pos]');
    if (!root) return;
    const cart = new Map();
    const list = root.querySelector('[data-pos-cart]');
    const form = root.querySelector('[data-pos-form]');
    const tax = root.querySelector('[data-pos-tax]');
    const method = root.querySelector('[data-pos-method]');
    const tender = root.querySelector('[data-pos-tender]');
    const reference = root.querySelector('[data-pos-reference]');
    const money = value => `CA$${value.toLocaleString('en-CA', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    const render = () => {
        list.replaceChildren();
        let subtotal = 0;
        [...cart.values()].forEach((item, index) => {
            subtotal += item.price * item.quantity;
            const row = document.createElement('div');
            row.className = 'parts-pos-cart-row';
            const label = document.createElement('div');
            const name = document.createElement('strong');
            name.textContent = item.name;
            const price = document.createElement('small');
            price.textContent = `${money(item.price)} each · ${item.available} available`;
            label.append(name, price);
            const quantity = document.createElement('input');
            quantity.type = 'number'; quantity.min = '1'; quantity.max = String(item.available);
            quantity.step = '1'; quantity.value = String(item.quantity);
            quantity.setAttribute('aria-label', `Quantity for ${item.name}`);
            quantity.addEventListener('change', () => {
                item.quantity = Math.max(1, Math.min(item.available, Math.trunc(Number(quantity.value)) || 1));
                render();
            });
            const quantityControl = document.createElement('div');
            quantityControl.className = 'parts-pos-quantity';
            const decrease = document.createElement('button');
            decrease.type = 'button'; decrease.textContent = '−';
            decrease.setAttribute('aria-label', `Decrease quantity of ${item.name}`);
            decrease.disabled = item.quantity <= 1;
            decrease.addEventListener('click', () => { item.quantity--; render(); });
            const increase = document.createElement('button');
            increase.type = 'button'; increase.textContent = '+';
            increase.setAttribute('aria-label', `Increase quantity of ${item.name}`);
            increase.disabled = item.quantity >= item.available;
            increase.addEventListener('click', () => { item.quantity++; render(); });
            quantityControl.append(decrease, quantity, increase);
            const total = document.createElement('output');
            total.textContent = money(item.price * item.quantity);
            const remove = document.createElement('button');
            remove.type = 'button'; remove.className = 'parts-pos-remove'; remove.textContent = '×';
            remove.setAttribute('aria-label', `Remove ${item.name}`);
            remove.addEventListener('click', () => { cart.delete(item.id); render(); });
            const idInput = document.createElement('input');
            idInput.type = 'hidden'; idInput.name = `Lines[${index}].PartId`; idInput.value = String(item.id);
            const qtyInput = document.createElement('input');
            qtyInput.type = 'hidden'; qtyInput.name = `Lines[${index}].Quantity`; qtyInput.value = String(item.quantity);
            row.append(label, quantityControl, total, remove, idInput, qtyInput);
            list.append(row);
        });
        if (!cart.size) {
            const empty = document.createElement('p');
            empty.className = 'parts-pos-empty'; empty.textContent = 'Add a part to start a sale.';
            list.append(empty);
        }
        root.querySelector('[data-pos-cart-count]').textContent = String([...cart.values()].reduce((count, item) => count + item.quantity, 0));
        const rate = Number(tax.selectedOptions[0]?.dataset.rate || 0);
        const taxAmount = Math.round((subtotal * rate + Number.EPSILON) * 100) / 100;
        const total = subtotal + taxAmount;
        const cash = method.value === 'Cash';
        root.querySelector('[data-pos-tender-wrap]').hidden = !cash;
        root.querySelector('[data-pos-reference-wrap]').hidden = cash;
        tender.required = cash; reference.required = !cash;
        root.querySelector('[data-pos-subtotal]').textContent = money(subtotal);
        root.querySelector('[data-pos-tax-total]').textContent = money(taxAmount);
        root.querySelector('[data-pos-total]').textContent = money(total);
        root.querySelector('[data-pos-change]').textContent = money(cash ? Math.max(0, Number(tender.value) - total) : 0);
        root.querySelector('[data-pos-submit]').disabled = !cart.size || (cash && Number(tender.value) < total);
    };
    const addPart = card => {
        const id = Number(card.dataset.id);
        const current = cart.get(id);
        if (current) current.quantity = Math.min(current.available, current.quantity + 1);
        else cart.set(id, { id, name: card.dataset.name, price: Number(card.dataset.price), available: Number(card.dataset.available), quantity: 1 });
        render();
    };
    root.querySelectorAll('[data-pos-add]').forEach(card => {
        card.addEventListener('click', () => addPart(card));
        card.addEventListener('keydown', event => {
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                addPart(card);
            }
        });
    });
    root.querySelector('[data-pos-clear]').addEventListener('click', () => { cart.clear(); render(); });
    root.querySelector('[data-pos-search]').addEventListener('input', event => {
        const query = event.target.value.trim().toLowerCase();
        let matches = 0;
        root.querySelectorAll('[data-pos-product]').forEach(product => {
            product.hidden = !product.dataset.search.includes(query);
            if (!product.hidden) matches++;
        });
        root.querySelector('[data-pos-no-results]').hidden = matches > 0 || !query;
    });
    tax.addEventListener('change', render); method.addEventListener('change', render); tender.addEventListener('input', render);
    form.addEventListener('submit', event => {
        if (!cart.size || (method.value === 'Cash' && Number(tender.value) < Number(root.querySelector('[data-pos-total]').textContent.replaceAll(',', '')))) event.preventDefault();
        else root.querySelector('[data-pos-submit]').disabled = true;
    });
    render();
})();
