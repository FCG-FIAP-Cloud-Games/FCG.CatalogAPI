/* Swagger UI 5.17.14 (Swashbuckle 6.9.0). Revalidate component/action contracts on upgrade.
 * Official plugin hooks only: no DOM selectors, spec rewriting or token persistence.
 */
(function (root) {
    "use strict";
    const path = "/api/v1/pedidos";
    const header = "Idempotency-Key";
    const endpoint = new URL("../api/v1/pedidos", root.location.href);
    const storageKey = "fcg.swagger.purchase.v1:" + endpoint.href;
    const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    const listeners = new Set();
    let message = "Execute cria ou repete a intenção atual. Após rejeição, use Nova intenção de compra antes de executar novamente.";

    function notify(value) {
        message = value;
        listeners.forEach(listener => listener(value));
    }
    function guid(value) {
        if (typeof value !== "string" || !uuid.test(value) || /^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(value))
            throw new Error("Informe um JogoId válido e autorize com um token contendo sub UUID válido.");
        return value.toLowerCase();
    }
    function context(token, body) {
        if (typeof token !== "string" || !token.trim())
            throw new Error("Use Authorize antes de criar ou repetir a compra.");
        // Decode solely to partition local UI state; JWT trust/authorization remains in the API.
        const encoded = token.replace(/^Bearer\s+/i, "").split(".")[1];
        if (!encoded) throw new Error("Token JWT inválido.");
        let claims;
        try {
            const base64 = encoded.replace(/-/g, "+").replace(/_/g, "/");
            claims = JSON.parse(decodeURIComponent(Array.from(root.atob(base64.padEnd(Math.ceil(base64.length / 4) * 4, "=")),
                c => "%" + c.charCodeAt(0).toString(16).padStart(2, "0")).join("")));
        } catch { throw new Error("Token JWT inválido."); }
        if (typeof claims.iss !== "string" || !claims.iss) throw new Error("Token sem emissor.");
        let data;
        try { data = typeof body === "string" ? JSON.parse(body) : body; }
        catch { throw new Error("Informe um corpo JSON válido com jogoId."); }
        return JSON.stringify([endpoint.href, claims.iss, guid(claims.sub), guid(data?.jogoId ?? data?.JogoId)]);
    }
    function newUuid() {
        if (root.crypto.randomUUID) return root.crypto.randomUUID();
        const bytes = root.crypto.getRandomValues(new Uint8Array(16));
        bytes[6] = (bytes[6] & 15) | 64;
        bytes[8] = (bytes[8] & 63) | 128;
        const hex = Array.from(bytes, b => b.toString(16).padStart(2, "0")).join("");
        return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
    }
    function intention(token, body, renew = false) {
        const scope = context(token, body);
        let previous;
        try {
            const stored = root.sessionStorage.getItem(storageKey);
            if (stored && !renew) {
                previous = JSON.parse(stored);
                if (typeof previous.context !== "string" || !uuid.test(previous.key) || previous.key === "00000000-0000-0000-0000-000000000000")
                    throw new Error("invalid state");
            }
        } catch {
            throw new Error("Não foi possível recuperar a intenção. Não repita uma compra de resultado desconhecido. Após verificar o pedido, use Nova intenção de compra.");
        }
        const reused = !renew && previous?.context === scope;
        const current = reused ? previous : { context: scope, key: newUuid() };
        try { root.sessionStorage.setItem(storageKey, JSON.stringify(current)); }
        catch { throw new Error("sessionStorage indisponível. Envio bloqueado para preservar a chave de retry."); }
        notify(reused ? "Intenção preservada: Execute repete a mesma compra."
            : "Nova intenção preparada para este usuário e jogo. Execute enviará a compra.");
        return current.key;
    }
    function isOperation(pathMethod) {
        return pathMethod?.[0] === path && pathMethod?.[1]?.toLowerCase() === "post";
    }
    function fromUi(system, renew) {
        const token = system.authSelectors.authorized().getIn(["Bearer", "value"]);
        const body = system.oas3Selectors.requestBodyValue(path, "post");
        return intention(token, body, renew);
    }
    root.PedidoIntencao = {
        intercept(request) {
            const url = new URL(request.url, root.location.href);
            if (request.method?.toUpperCase() !== "POST" || url.origin !== endpoint.origin || url.pathname !== endpoint.pathname)
                return request;
            try {
                const headers = request.headers || {};
                const authorization = Object.keys(headers).find(name => name.toLowerCase() === "authorization");
                const key = intention(headers[authorization], request.body);
                Object.keys(headers).filter(name => name.toLowerCase() === header.toLowerCase()).forEach(name => delete headers[name]);
                headers[header] = key;
                request.headers = headers;
                return request;
            } catch (error) { notify(error.message); throw error; }
        }
    };
    root.PedidoIntencaoPlugin = function (system) {
        const React = system.React;
        return {
            statePlugins: {
                spec: {
                    wrapActions: {
                        validateParams: original => pathMethod => {
                            if (isOperation(pathMethod)) {
                                const parameter = system.specSelectors.specJson().getIn(["paths", path, "post", "parameters"])
                                    ?.find(p => p.get("in") === "header" && p.get("name").toLowerCase() === header.toLowerCase());
                                if (parameter) {
                                    let value = "";
                                    try { value = fromUi(system, false); }
                                    catch (error) { notify(error.message); }
                                    // Populate before Swagger's required-parameter validation, not only at transport time.
                                    system.specActions.changeParamByIdentity(pathMethod, parameter, value);
                                }
                            }
                            return original(pathMethod);
                        }
                    }
                }
            },
            wrapComponents: {
                parameterRow: Original => props => isOperation(props.pathMethod)
                    && props.param.get("in") === "header" && props.param.get("name").toLowerCase() === header.toLowerCase()
                    ? null : React.createElement(Original, props),
                execute: Original => class extends React.Component {
                    constructor(props) {
                        super(props);
                        this.state = { message };
                        this.update = value => this.setState({ message: value });
                    }
                    componentDidMount() { listeners.add(this.update); }
                    componentWillUnmount() { listeners.delete(this.update); }
                    render() {
                        if (!isOperation([this.props.path, this.props.method])) return React.createElement(Original, this.props);
                        return React.createElement("div", null,
                            React.createElement(Original, this.props),
                            React.createElement("button", {
                                type: "button", className: "btn", disabled: this.props.disabled,
                                onClick: () => {
                                    if (!root.confirm("Iniciar uma nova intenção? Para timeout ou resultado ainda desconhecido, cancele e use Execute para repetir a intenção atual.")) return;
                                    try { fromUi(system, true); }
                                    catch (error) { notify(error.message); }
                                }
                            }, "Nova intenção de compra"),
                            React.createElement("p", { role: "status", "aria-live": "polite" }, this.state.message),
                            React.createElement("p", null, "Após recarregar, autorize a mesma conta e informe o mesmo jogo para repetir. Trocar conta ou jogo inicia outra intenção; não faça isso enquanto o resultado anterior for desconhecido."));
                    }
                }
            }
        };
    };
})(window);
