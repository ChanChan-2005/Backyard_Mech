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
    const maxMoneyCents = 999999999999999999n;
    let totalCents = 0n;
    const scaledDecimal = (value, places) => {
        const raw = String(value).trim();
        const match = (raw.startsWith('.') ? `0${raw}` : raw).match(/^(\d+)(?:\.(\d+))?$/);
        if (!match || (match[2]?.length || 0) > places) return null;
        return BigInt(match[1]) * 10n ** BigInt(places) + BigInt((match[2] || '').padEnd(places, '0'));
    };
    const cents = value => scaledDecimal(value, 2);
    const money = value => `CA$${(value / 100n).toLocaleString('en-CA')}.${(value % 100n).toString().padStart(2, '0')}`;
    const render = () => {
        list.replaceChildren();
        let subtotal = 0n;
        [...cart.values()].forEach((item, index) => {
            subtotal += item.price * BigInt(item.quantity);
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
            total.textContent = money(item.price * BigInt(item.quantity));
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
        const rate = scaledDecimal(tax.selectedOptions[0]?.dataset.rate || '0', 4) || 0n;
        const taxAmount = (subtotal * rate + 5000n) / 10000n;
        totalCents = subtotal + taxAmount;
        const tenderCents = cents(tender.value) || 0n;
        const cash = method.value === 'Cash';
        root.querySelector('[data-pos-tender-wrap]').hidden = !cash;
        root.querySelector('[data-pos-reference-wrap]').hidden = cash;
        tender.required = cash; reference.required = !cash;
        root.querySelector('[data-pos-subtotal]').textContent = money(subtotal);
        root.querySelector('[data-pos-tax-total]').textContent = money(taxAmount);
        root.querySelector('[data-pos-total]').textContent = money(totalCents);
        root.querySelector('[data-pos-change]').textContent = money(cash && tenderCents > totalCents ? tenderCents - totalCents : 0n);
        root.querySelector('[data-pos-submit]').disabled = !cart.size || totalCents > maxMoneyCents || (cash && tenderCents < totalCents);
    };
    const addPart = card => {
        const id = Number(card.dataset.id);
        const current = cart.get(id);
        if (current) current.quantity = Math.min(current.available, current.quantity + 1);
        else cart.set(id, { id, name: card.dataset.name, price: cents(card.dataset.price) || 0n, available: Number(card.dataset.available), quantity: 1 });
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
        if (!cart.size || totalCents > maxMoneyCents || (method.value === 'Cash' && (cents(tender.value) || 0n) < totalCents)) event.preventDefault();
        else root.querySelector('[data-pos-submit]').disabled = true;
    });
    render();
})();
