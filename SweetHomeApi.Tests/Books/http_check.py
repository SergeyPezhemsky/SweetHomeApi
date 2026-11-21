"""HTTP regression suite for an isolated running SweetHome API and PostgreSQL.

Set BOOKS_QA_ADMIN_EMAIL and BOOKS_QA_ADMIN_PASSWORD for an approved test administrator.
"""
import json
import os
import pathlib
import time
import urllib.error
import urllib.parse
import urllib.request
from http.cookies import SimpleCookie

BASE = os.environ.get('BOOKS_QA_BASE_URL', 'http://127.0.0.1:5099').rstrip('/')
REGISTRATION_REQUESTS = '/api/Account/RegistrationRequests'
results = []

def call(method, path, data=None, cookie=None, expected=200, label=None):
    body = None if data is None else json.dumps(data, ensure_ascii=False).encode('utf-8')
    headers = {'Content-Type': 'application/json'}
    if cookie:
        headers['Cookie'] = cookie
    request = urllib.request.Request(BASE + path, data=body, headers=headers, method=method)
    try:
        response = urllib.request.urlopen(request, timeout=15)
    except urllib.error.HTTPError as error:
        response = error
    raw = response.read().decode('utf-8')
    value = json.loads(raw) if raw else None
    assert response.code == expected, (method, path, response.code, expected, raw)
    results.append({'label': label or method + ' ' + path, 'status': response.code})
    parsed = SimpleCookie()
    for header in response.headers.get_all('Set-Cookie', []):
        parsed.load(header)
    new_cookie = '; '.join(key + '=' + item.value for key, item in parsed.items())
    return value, new_cookie


def register_and_login(account, admin_cookie):
    _, registration_cookie = call('POST', '/api/Account/Register', account, label='register pending test user')
    assert not registration_cookie
    credentials = {'email': account['email'], 'password': account['password']}
    _, pending_cookie = call('POST', '/api/Account/Login', credentials, expected=403, label='pending login denied')
    assert not pending_cookie
    requests, _ = call('GET', REGISTRATION_REQUESTS, cookie=admin_cookie, label='list pending registrations')
    user_id = next(item['id'] for item in requests if item['email'] == account['email'])
    call('PUT', REGISTRATION_REQUESTS + '/' + user_id, {'status': 'Approved'}, admin_cookie, 204, 'approve test user')
    _, cookie = call('POST', '/api/Account/Login', credentials, label='approved login issues auth cookie')
    assert cookie
    return cookie


suffix = str(time.time_ns())
password = 'BooksQa!2026Pass123'
account = {'email': 'books.owner.' + suffix + '@example.test', 'name': 'books_owner_' + suffix,
           'password': password, 'confirmPassword': password}
other = {'email': 'books.other.' + suffix + '@example.test', 'name': 'books_other_' + suffix,
         'password': password, 'confirmPassword': password}

call('GET', '/api/v1/books', expected=401, label='unauthenticated books rejected')
_, admin_cookie = call('POST', '/api/Account/Login', {
    'email': os.environ['BOOKS_QA_ADMIN_EMAIL'],
    'password': os.environ['BOOKS_QA_ADMIN_PASSWORD'],
}, label='test administrator login')
assert admin_cookie
owner_cookie = register_and_login(account, admin_cookie)
owner, _ = call('GET', '/api/Account/User', cookie=owner_cookie, label='owner login cookie works')
assert owner['name'] == account['name']
other_cookie = register_and_login(other, admin_cookie)
empty, _ = call('GET', '/api/v1/books', cookie=owner_cookie, label='empty library stats')
assert empty['items'] == [] and empty['stats']['total'] == 0 and empty['total'] == 0

data = {'title': '  Дюна_%  ', 'author': '  Фрэнк Герберт  ', 'genre': '  Фантастика ',
        'status': 'READING', 'totalPages': 640, 'pagesRead': 120, 'rating': 5, 'notes': '  Заметка  '}
book, _ = call('POST', '/api/v1/books', data, owner_cookie, 201, 'create trims strings and persists book')
book_id = book['id']
assert book['title'] == 'Дюна_%' and book['author'] == 'Фрэнк Герберт' and book['genre'] == 'Фантастика'
assert book['notes'] == 'Заметка' and book['pagesRead'] == 120 and book['rating'] == 5
assert 'userId' not in book and 'user' not in book
assert book['createdAt'].endswith('Z') and book['updatedAt'].endswith('Z')
loaded, _ = call('GET', '/api/v1/books/' + book_id, cookie=owner_cookie, label='read persisted book')
assert {key: value for key, value in loaded.items() if key not in ('createdAt', 'updatedAt')} == {key: value for key, value in book.items() if key not in ('createdAt', 'updatedAt')}
book = loaded  # PostgreSQL timestamps retain microseconds, whereas an immediate .NET response can include ticks.
for query in ['гЕрБеРт', 'дЮнА', '_%']:
    found, _ = call('GET', '/api/v1/books?query=' + urllib.parse.quote(query), cookie=owner_cookie, label='literal case-insensitive search ' + query)
    assert found['total'] == 1, (query, found)
missing, _ = call('GET', '/api/v1/books?query=' + urllib.parse.quote('xyz_%'), cookie=owner_cookie, label='literal search has no false positives')
assert missing['total'] == 0 and missing['stats']['total'] == 1 and missing['stats']['pagesRead'] == 120, missing

second = {'title': 'Алиса в Стране чудес', 'author': 'Льюис Кэрролл', 'status': 'WANT_TO_READ', 'pagesRead': 0}
another, _ = call('POST', '/api/v1/books', second, owner_cookie, 201, 'create second book with unknown page count')
filtered, _ = call('GET', '/api/v1/books?status=READING&pageSize=1', cookie=owner_cookie, label='filtered list keeps global stats')
assert filtered['total'] == 1 and len(filtered['items']) == 1
assert filtered['stats'] == {'total': 2, 'wantToRead': 1, 'reading': 1, 'completed': 0, 'onHold': 0, 'dropped': 0, 'pagesRead': 120}
combined, _ = call('GET', '/api/v1/books?query=' + urllib.parse.quote('Герберт') + '&status=WANT_TO_READ&sortBy=TITLE&pageSize=1', cookie=owner_cookie, label='regression: search query binds together with status and pagination')
assert combined['items'] == [] and combined['total'] == 0 and combined['pageSize'] == 1 and combined['stats']['total'] == 2
page1, _ = call('GET', '/api/v1/books?sortBy=TITLE&pageSize=1', cookie=owner_cookie, label='stable title sort first page')
assert page1['items'][0]['id'] == another['id'] and page1['hasNext'] is True
page2, _ = call('GET', '/api/v1/books?sortBy=TITLE&pageSize=1&page=2', cookie=owner_cookie, label='pagination second page')
assert page2['items'][0]['id'] == book_id and page2['hasNext'] is False

foreign, _ = call('GET', '/api/v1/books', cookie=other_cookie, label='other user sees empty library and stats')
assert foreign['items'] == [] and foreign['stats']['total'] == 0
call('GET', '/api/v1/books/' + book_id, cookie=other_cookie, expected=404, label='other user cannot read book')
call('PUT', '/api/v1/books/' + book_id, data, other_cookie, 404, 'other user cannot update book')
call('DELETE', '/api/v1/books/' + book_id, cookie=other_cookie, expected=404, label='other user cannot delete book')
data['status'] = 'COMPLETED'
completed, _ = call('PUT', '/api/v1/books/' + book_id, data, owner_cookie, label='completion fills known page count')
assert completed['pagesRead'] == 640 and completed['createdAt'] == book['createdAt']
completed_list, _ = call('GET', '/api/v1/books?status=COMPLETED', cookie=owner_cookie, label='completion persists and updates global statistics')
assert completed_list['stats']['completed'] == 1 and completed_list['stats']['pagesRead'] == 640
for status in ['ON_HOLD', 'DROPPED', 'READING', 'WANT_TO_READ']:
    data['status'] = status
    current, _ = call('PUT', '/api/v1/books/' + book_id, data, owner_cookie, label='persist status ' + status)
    assert current['status'] == status
    listed, _ = call('GET', '/api/v1/books?status=' + status, cookie=owner_cookie, label='filter status ' + status)
    assert any(item['id'] == book_id for item in listed['items'])
bad = dict(data, totalPages=100, pagesRead=101)
validation, _ = call('POST', '/api/v1/books', bad, owner_cookie, 400, 'reject progress above length')
assert 'pagesRead' in validation['errors']
for value in [{'title': ' '}, {'author': ''}, {'status': 99}, {'rating': 6}, {'totalPages': 0}, {'pagesRead': -1}]:
    invalid = dict(data, **value)
    call('POST', '/api/v1/books', invalid, owner_cookie, 400, 'reject invalid ' + next(iter(value)))
call('GET', '/api/v1/books?status=UNKNOWN', cookie=owner_cookie, expected=400, label='reject invalid status query')
call('GET', '/api/v1/books?sortBy=999', cookie=owner_cookie, expected=400, label='reject unknown numeric sort query')
bounded, _ = call('GET', '/api/v1/books?page=-1&pageSize=999', cookie=owner_cookie, label='pagination clamps requested bounds')
assert bounded['page'] == 1 and bounded['pageSize'] == 100
call('DELETE', '/api/v1/books/' + book_id, cookie=owner_cookie, expected=204, label='delete own book')
call('GET', '/api/v1/books/' + book_id, cookie=owner_cookie, expected=404, label='deleted book inaccessible')
call('DELETE', '/api/v1/books/' + another['id'], cookie=owner_cookie, expected=204, label='delete second own book')
final, _ = call('GET', '/api/v1/books', cookie=owner_cookie, label='deletion resets all own library stats')
assert final['total'] == 0 and final['stats']['total'] == 0 and final['stats']['pagesRead'] == 0
report = pathlib.Path(os.environ.get('BOOKS_QA_REPORT', '.tmp/books-http-results.json'))
report.parent.mkdir(parents=True, exist_ok=True)
report.write_text(json.dumps({'passed': len(results), 'results': results}, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps({'passed': len(results), 'resultFile': str(report)}, ensure_ascii=False))
