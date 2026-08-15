const form = document.querySelector('#message-form');
const input = document.querySelector('#message');
const sendButton = document.querySelector('#send');
const stopButton = document.querySelector('#stop');
const statusElement = document.querySelector('#status');
const messages = document.querySelector('#messages');

let conversationId;
let activeRequest;

function setStatus(state) {
  statusElement.dataset.state = state;
  statusElement.textContent = state.charAt(0).toUpperCase() + state.slice(1) + (state === 'generating' ? '...' : '');
}

function addMessage(role, text = '') {
  const element = document.createElement('div');
  element.className = `message ${role}`;
  element.textContent = text;
  messages.appendChild(element);
  messages.scrollTop = messages.scrollHeight;
  return element;
}

async function ensureConversation(signal) {
  if (conversationId) return conversationId;
  const response = await fetch('/api/conversations', { method: 'POST', signal });
  if (!response.ok) throw new Error('Unable to create a conversation.');
  conversationId = (await response.json()).conversationId;
  return conversationId;
}

async function readUpdates(response, assistantMessage, request) {
  const reader = response.body.getReader();
  request.reader = reader;
  const decoder = new TextDecoder();
  let pending = '';

  while (true) {
    const { value, done } = await reader.read();
    if (request.cancelled) break;
    pending += decoder.decode(value || new Uint8Array(), { stream: !done });
    const lines = pending.split('\n');
    pending = done ? '' : lines.pop();

    for (const line of lines) {
      if (!line.trim()) continue;
      const update = JSON.parse(line);
      if (update.delta) {
        assistantMessage.textContent += update.delta;
        messages.scrollTop = messages.scrollHeight;
      }
      setStatus(update.status);
    }

    if (done) break;
  }
}

form.addEventListener('submit', async event => {
  event.preventDefault();
  const message = input.value.trim();
  if (!message || activeRequest) return;

  addMessage('user', message);
  const assistantMessage = addMessage('assistant');
  input.value = '';
  const request = {
    controller: new AbortController(),
    reader: undefined,
    cancelled: false
  };
  activeRequest = request;
  sendButton.disabled = true;
  stopButton.hidden = false;
  setStatus('generating');

  try {
    const id = await ensureConversation(request.controller.signal);
    const response = await fetch(`/api/conversations/${id}/messages/stream`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message }),
      signal: request.controller.signal
    });
    if (!response.ok || !response.body) throw new Error('Streaming request failed.');
    await readUpdates(response, assistantMessage, request);
    if (request.cancelled) assistantMessage.remove();
  } catch (error) {
    if (request.cancelled) {
      setStatus('cancelled');
      assistantMessage.remove();
    } else {
      setStatus('failed');
      assistantMessage.textContent ||= 'The response could not be completed.';
    }
  } finally {
    if (activeRequest === request) activeRequest = undefined;
    sendButton.disabled = false;
    stopButton.hidden = true;
    input.focus();
  }
});

stopButton.addEventListener('click', () => {
  if (!activeRequest) return;
  activeRequest.cancelled = true;
  setStatus('cancelled');
  if (activeRequest.reader) {
    activeRequest.reader.cancel().catch(() => {});
  } else {
    activeRequest.controller.abort();
  }
});
