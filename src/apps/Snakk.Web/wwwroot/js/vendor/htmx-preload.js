(function() {
  /**
   * This adds the "preload" extension to htmx. The extension will
    * preload the targets of elements with "preload" attribute if:
    * - they also have `href`, `hx-get` or `data-hx-get` attributes
    * - they are radio buttons, checkboxes, select elements and submit
    *   buttons of forms with `method="get"` or `hx-get` attributes
    * The extension relies on browser cache and for it to work
    * server response must include `Cache-Control` header
    * e.g. `Cache-Control: private, max-age=60`.
    * For more details @see https://htmx.org/extensions/preload/
  */

  htmx.defineExtension('preload', {
    onEvent: function(name, event) {
      // Process preload attributes on `htmx:afterProcessNode`
      if (name === 'htmx:afterProcessNode') {
        // Initialize all nodes with `preload` attribute
        const parent = event.target || event.detail.elt;
        const preloadNodes = [
          ...parent.hasAttribute("preload") ? [parent] : [],
          ...parent.querySelectorAll("[preload]")]
        preloadNodes.forEach(function(node) {
          // Initialize the node with the `preload` attribute
          init(node)

          // Initialize all child elements which has
          // `href`, `hx-get` or `data-hx-get` attributes
          node.querySelectorAll('[href],[hx-get],[data-hx-get]').forEach(init)
        })
        return
      }

      // Intercept HTMX preload requests on `htmx:beforeRequest` and
      // send them as XHR requests instead to avoid side-effects,
      // such as showing loading indicators while preloading data.
      if (name === 'htmx:beforeRequest') {
        const requestHeaders = event.detail.requestConfig.headers
        if (!("HX-Preloaded" in requestHeaders
              && requestHeaders["HX-Preloaded"] === "true")) {
          return
        }

        event.preventDefault()
        // Reuse XHR created by HTMX with replaced callbacks
        const xhr = event.detail.xhr
        xhr.onload = function() {
          processResponse(event.detail.elt, xhr.responseText)
        }
        xhr.onerror = null
        xhr.onabort = null
        xhr.ontimeout = null
        xhr.send()
      }
    }
  })

  function init(node) {
    if (node.preloadState !== undefined) {
      return
    }

    if (!isValidNodeForPreloading(node)) {
      return
    }

    if (node instanceof HTMLFormElement) {
      const form = node
      if (!((form.hasAttribute('method') && form.method === 'get')
        || form.hasAttribute('hx-get') || form.hasAttribute('hx-data-get'))) {
        return
      }
      for (let i = 0; i < form.elements.length; i++) {
        const element = form.elements.item(i);
        init(element);
        element.labels.forEach(init);
      }
      return
    }

    let preloadAttr = getClosestAttribute(node, 'preload');
    node.preloadAlways = preloadAttr && preloadAttr.includes('always');
    if (node.preloadAlways) {
      preloadAttr = preloadAttr.replace('always', '').trim();
    }
    let triggerEventName = preloadAttr || 'mousedown';

    const needsTimeout = triggerEventName === 'mouseover'
    node.addEventListener(triggerEventName, getEventHandler(node, needsTimeout))

    if (triggerEventName === 'mousedown' || triggerEventName === 'mouseover') {
      node.addEventListener('touchstart', getEventHandler(node))
    }

    if (triggerEventName === 'mouseover') {
      node.addEventListener('mouseout', function(evt) {
        if ((evt.target === node) && (node.preloadState === 'TIMEOUT')) {
          node.preloadState = 'READY'
        }
      })
    }

    node.preloadState = 'READY'
    htmx.trigger(node, 'preload:init')
  }

  function getEventHandler(node, needsTimeout = false) {
    return function() {
      if (node.preloadState !== 'READY') {
        return
      }

      if (needsTimeout) {
        node.preloadState = 'TIMEOUT'
        const timeoutMs = 100
        window.setTimeout(function() {
          if (node.preloadState === 'TIMEOUT') {
            node.preloadState = 'READY'
            load(node)
          }
        }, timeoutMs)
        return
      }

      load(node)
    }
  }

  function load(node) {
    if (node.preloadState !== 'READY') {
      return
    }
    node.preloadState = 'LOADING'

    const hxGet = node.getAttribute('hx-get') || node.getAttribute('data-hx-get')
    if (hxGet) {
      sendHxGetRequest(hxGet, node);
      return
    }

    const hxBoost = getClosestAttribute(node, "hx-boost") === "true"
    if (node.hasAttribute('href')) {
      const url = node.getAttribute('href');
      if (hxBoost) {
        sendHxGetRequest(url, node);
      } else {
        sendXmlGetRequest(url, node);
      }
      return
    }

    if (isPreloadableFormElement(node)) {
      const url = node.form.getAttribute('action')
                  || node.form.getAttribute('hx-get')
                  || node.form.getAttribute('data-hx-get');
      const formData = htmx.values(node.form);
      const isStandardForm = !(node.form.getAttribute('hx-get')
                              || node.form.getAttribute('data-hx-get')
                              || hxBoost);
      const sendGetRequest = isStandardForm ? sendXmlGetRequest : sendHxGetRequest

      if (node.type === 'submit') {
        sendGetRequest(url, node.form, formData)
        return
      }

      const inputName = node.name || node.control.name;
      if (node.tagName === 'SELECT') {
        Array.from(node.options).forEach(option => {
          if (option.selected) return;
          formData.set(inputName, option.value);
          const formDataOrdered = forceFormDataInOrder(node.form, formData);
          sendGetRequest(url, node.form, formDataOrdered)
        });
        return
      }

      const inputType = node.getAttribute("type") || node.control.getAttribute("type");
      const nodeValue = node.value || node.control?.value;
      if (inputType === 'radio') {
        formData.set(inputName, nodeValue);
      } else if (inputType === 'checkbox'){
        const inputValues = formData.getAll(inputName);
        if (inputValues.includes(nodeValue)) {
          formData[inputName] = inputValues.filter(value => value !== nodeValue);
        } else {
          formData.append(inputName, nodeValue);
        }
      }
      const formDataOrdered = forceFormDataInOrder(node.form, formData);
      sendGetRequest(url, node.form, formDataOrdered)
      return
    }
  }

  function forceFormDataInOrder(form, formData) {
    const formElements = form.elements;
    const orderedFormData = new FormData();
    for(let i = 0; i < formElements.length; i++) {
      const element = formElements.item(i);
      if (formData.has(element.name) && element.tagName === 'SELECT') {
        orderedFormData.append(
          element.name, formData.get(element.name));
        continue;
      }
      if (formData.has(element.name) && formData.getAll(element.name)
        .includes(element.value)) {
        orderedFormData.append(element.name, element.value);
      }
    }
    return orderedFormData;
  }

  function sendHxGetRequest(url, sourceNode, formData = undefined) {
    htmx.ajax('GET', url, {
      source: sourceNode,
      values: formData,
      headers: {"HX-Preloaded": "true"}
    });
  }

  function sendXmlGetRequest(url, sourceNode, formData = undefined) {
    const xhr = new XMLHttpRequest()
    if (formData) {
      url += '?' + new URLSearchParams(formData.entries()).toString()
    }
    xhr.open('GET', url);
    xhr.setRequestHeader("HX-Preloaded", "true")
    xhr.onload = function() { processResponse(sourceNode, xhr.responseText) }
    xhr.send()
  }

  function processResponse(node, responseText) {
    node.preloadState = node.preloadAlways ? 'READY' : 'DONE'

    if (getClosestAttribute(node, 'preload-images') === 'true') {
      document.createElement('div').innerHTML = responseText
    }
  }

  function getClosestAttribute(node, attribute) {
    if (node == undefined) { return undefined }
    return node.getAttribute(attribute)
      || node.getAttribute('data-' + attribute)
      || getClosestAttribute(node.parentElement, attribute)
  }

  function isValidNodeForPreloading(node) {
    const getReqAttrs = ['href', 'hx-get', 'data-hx-get'];
    const includesGetRequest = node => getReqAttrs.some(a => node.hasAttribute(a))
                                        || node.method === 'get';
    const isPreloadableGetFormElement = node.form instanceof HTMLFormElement
                                        && includesGetRequest(node.form)
                                        && isPreloadableFormElement(node)
    if (!includesGetRequest(node) && !isPreloadableGetFormElement) {
      return false
    }

    if (node instanceof HTMLInputElement && node.closest('label')) {
      return false
    }

    return true
  }

  function isPreloadableFormElement(node) {
    if (node instanceof HTMLInputElement || node instanceof HTMLButtonElement) {
      const type = node.getAttribute('type');
      return ['checkbox', 'radio', 'submit'].includes(type);
    }
    if (node instanceof HTMLLabelElement) {
      return node.control && isPreloadableFormElement(node.control);
    }
    return node instanceof HTMLSelectElement;
  }
})()
