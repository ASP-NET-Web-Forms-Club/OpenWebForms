// Vanilla CRUD — API + Fetch. Server renders the list HTML; JS just shuttles data.
const API_URL = 'BooksApi.aspx';

document.addEventListener('DOMContentLoaded', function () {
    loadBooks();
});

function showMsg(text, ok) {
    const el = document.getElementById('msg');
    if (!text) { el.innerHTML = ''; return; }
    el.innerHTML = '<div class="msg ' + (ok ? 'msg-success' : 'msg-error') + '">' + text + '</div>';
}

// GET list — server returns a ready-to-inject HTML fragment.
async function loadBooks() {
    try {
        const response = await fetch(API_URL + '?action=list');
        const html = await response.text();
        document.getElementById('book-list').innerHTML = html;
    } catch (error) {
        console.error('loadBooks error:', error);
        document.getElementById('book-list').innerHTML =
            '<div class="empty">Failed to load books.</div>';
    }
}

// POST save (insert or update depending on hidden id).
async function saveBook() {
    const formData = new FormData();
    formData.append('action', 'save');
    formData.append('id', document.getElementById('book-id').value);
    formData.append('title', document.getElementById('title').value);
    formData.append('author', document.getElementById('author').value);
    formData.append('genre', document.getElementById('genre').value);
    formData.append('year', document.getElementById('year').value);
    formData.append('in_stock', document.getElementById('in_stock').checked ? '1' : '0');

    try {
        const response = await fetch(API_URL, { method: 'POST', body: formData });
        const data = await response.json();
        if (data.success) {
            showMsg(data.message, true);
            clearForm();
            loadBooks();
        } else {
            showMsg(data.message, false);
        }
    } catch (error) {
        console.error('saveBook error:', error);
        showMsg('Save failed.', false);
    }
}

// GET one book → fill the form for editing.
async function editBook(id) {
    try {
        const response = await fetch(API_URL + '?action=get&id=' + id);
        const data = await response.json();
        if (!data.success) { showMsg(data.message, false); return; }

        const b = data.item;
        document.getElementById('book-id').value = b.Id;
        document.getElementById('title').value = b.Title;
        document.getElementById('author').value = b.Author;
        document.getElementById('genre').value = b.Genre;
        document.getElementById('year').value = b.Year;
        document.getElementById('in_stock').checked = (b.InStock === 1);
        showMsg('', true);
        window.scrollTo(0, 0);
    } catch (error) {
        console.error('editBook error:', error);
        showMsg('Could not load book.', false);
    }
}

// POST delete (with confirm).
async function deleteBook(id) {
    if (!confirm('Delete this book?')) return;

    const formData = new FormData();
    formData.append('action', 'delete');
    formData.append('id', id);

    try {
        const response = await fetch(API_URL, { method: 'POST', body: formData });
        const data = await response.json();
        if (data.success) {
            showMsg(data.message, true);
            loadBooks();
        } else {
            showMsg(data.message, false);
        }
    } catch (error) {
        console.error('deleteBook error:', error);
        showMsg('Delete failed.', false);
    }
}

function clearForm() {
    document.getElementById('book-id').value = '';
    document.getElementById('title').value = '';
    document.getElementById('author').value = '';
    document.getElementById('genre').value = 'Fiction';
    document.getElementById('year').value = '';
    document.getElementById('in_stock').checked = false;
}
