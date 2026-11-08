// Run with node --test tests/SwaggerUI/pedido-intencao.test.cjs (no npm dependencies).
const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const source = fs.readFileSync(path.join(__dirname, '../../src/FCG.Catalog.Api/wwwroot/swagger/pedido-intencao.js'), 'utf8');
const user = '11111111-1111-4111-8111-111111111111';
const game = '22222222-2222-4222-8222-222222222222';
const other = '33333333-3333-4333-8333-333333333333';
const token = (sub = user, extra = {}) => 'eyJhbGciOiJSUzI1NiJ9.' + Buffer.from(JSON.stringify({ iss: 'fcg', sub, ...extra })).toString('base64url') + '.test';
const store = () => {
    const values = new Map();
    return { values, getItem: k => values.get(k) ?? null, setItem: (k, v) => values.set(k, v) };
};
function load(storage = store()) {
    const root = { location: { href: 'http://localhost/swagger/index.html' }, sessionStorage: storage,
        crypto: crypto.webcrypto, atob: v => Buffer.from(v, 'base64').toString('binary'), confirm: () => true };
    vm.runInNewContext(source, { window: root, URL, Uint8Array });
    return root;
}
function request(sub = user, jogoId = game, extra = {}) {
    return { url: 'http://localhost/api/v1/pedidos', method: 'POST',
        headers: { Authorization: 'Bearer ' + token(sub), 'idempotency-key': 'ignored-example' }, body: JSON.stringify({ jogoId }), ...extra };
}
const send = (ui, req = request()) => ui.PedidoIntencao.intercept(req).headers['Idempotency-Key'];

test('retry, lost response and reload preserve the key without persisting JWT', () => {
    const storage = store();
    const ui = load(storage);
    const first = send(ui);
    assert.match(first, /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
    assert.equal(send(ui), first);
    assert.equal(send(load(storage)), first);
    assert.equal(send(ui, request(user, game, { headers: { Authorization: 'Bearer ' + token(user, { exp: 9999999999 }) } })), first);
    assert.equal([...storage.values.values()].some(v => v.includes(token())), false);
});

test('game, account and issuer changes cannot reuse the previous key', () => {
    const ui = load();
    const a = send(ui);
    const b = send(ui, request(user, other));
    const c = send(ui, request(other, other));
    const d = send(ui, request(other, other, { headers: { Authorization: 'Bearer ' + token(other, { iss: 'other' }) } }));
    assert.equal(new Set([a, b, c, d]).size, 4);
    assert.notEqual(send(ui), a);
});

function system() {
    const parameter = { get: key => ({ name: 'Idempotency-Key', in: 'header' })[key] };
    const s = { React: { Component: class { constructor(props) { this.props = props; } setState(state) { this.state = state; } },
        createElement: (type, props, ...children) => ({ type, props, children }) },
        authSelectors: { authorized: () => ({ getIn: () => token() }) },
        oas3Selectors: { requestBodyValue: () => JSON.stringify({ jogoId: game }) },
        specSelectors: { specJson: () => ({ getIn: () => [parameter] }) },
        specActions: { changeParamByIdentity: (pm, p, value) => { s.value = value; } } };
    return { s, parameter };
}

test('required validation gets the real key, only target row is hidden, explicit new intention rotates', () => {
    const ui = load();
    const { s, parameter } = system();
    const plugin = ui.PedidoIntencaoPlugin(s);
    let validated;
    const validate = plugin.statePlugins.spec.wrapActions.validateParams(pm => { validated = s.value; return pm; });
    validate(['/api/v1/pedidos', 'post']);
    assert.equal(validated, send(ui));
    const first = validated;
    validate(['/api/v1/pedidos', 'post']);
    assert.equal(validated, first);
    const Original = () => {};
    const row = plugin.wrapComponents.parameterRow(Original);
    assert.equal(row({ pathMethod: ['/api/v1/pedidos', 'post'], param: parameter }), null);
    assert.equal(row({ pathMethod: ['/other', 'post'], param: parameter }).type, Original);
    const Execute = plugin.wrapComponents.execute(Original);
    const view = new Execute({ path: '/api/v1/pedidos', method: 'post' });
    view.componentDidMount();
    const button = view.render().children[1];
    ui.confirm = () => false;
    button.props.onClick();
    assert.equal(send(ui), first);
    ui.confirm = () => true;
    button.props.onClick(); // Also the required action for a new attempt after Rejected.
    const second = send(ui);
    assert.notEqual(second, first);
    assert.equal(send(ui), second);
    view.componentWillUnmount();
});

test('other operations and remote destinations pass through unchanged; one outgoing header', () => {
    const ui = load();
    for (const extra of [{ method: 'GET' }, { url: 'http://localhost/api/v1/jogos' }, { url: 'https://other/api/v1/pedidos' }]) {
        const req = request(user, game, extra);
        const before = JSON.stringify(req);
        assert.equal(ui.PedidoIntencao.intercept(req), req);
        assert.equal(JSON.stringify(req), before);
    }
    const req = request();
    send(ui, req);
    assert.deepEqual(Object.keys(req.headers).filter(k => k.toLowerCase() === 'idempotency-key'), ['Idempotency-Key']);
});

test('missing identity, invalid body, unavailable storage and corrupt state fail closed', () => {
    assert.throws(() => send(load(), request(user, game, { headers: {} })), /Authorize/);
    assert.throws(() => send(load(), request(user, game, { body: '{' })), /JSON/);
    assert.throws(() => send(load(), request(user, '00000000-0000-0000-0000-000000000000')), /UUID/);
    assert.throws(() => send(load({ getItem: () => null, setItem: () => { throw Error('denied'); } })), /sessionStorage/);
    assert.throws(() => send(load({ getItem: () => '{broken', setItem: () => {} })), /recuperar/);
});
