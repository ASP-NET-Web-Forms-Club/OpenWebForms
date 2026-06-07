#!/bin/bash
SRC='/mnt/d/Claude Files/OpenWebForms/samples/webforms'
pkill -f 'SampleHost.dll 7013' 2>/dev/null
sleep 1
rm -f ~/owf/app3/wwwroot/*
cp "$SRC/_wsl/app3/Global.asax" ~/owf/app3/wwwroot/
cp "$SRC/_shared/site.css" ~/owf/app3/wwwroot/
cd ~/owf/app3
(nohup dotnet SampleHost.dll 7013 > ~/owf/app3.log 2>&1 &)
sleep 5
curl -s -o /dev/null http://localhost:7013/      # warm compile
echo "=== GET / (page) ==="
curl -s -o /tmp/page.html -w 'HTTP %{http_code} bytes=%{size_download}\n' http://localhost:7013/
grep -o 'Book Library\|book-list\|saveBook\|Pageless' /tmp/page.html | sort | uniq -c
echo "=== GET /site.css ==="
curl -s -o /dev/null -w 'HTTP %{http_code} bytes=%{size_download}\n' http://localhost:7013/site.css
echo "=== list (seed) ==="
curl -s -w '\n[HTTP %{http_code}]\n' "http://localhost:7013/bookapi?action=list" | grep -o 'The Hobbit\|Dune\|Clean Code\|Pragmatic\|badge-yes\|badge-no\|HTTP 200' | sort | uniq -c
echo "=== create ==="
curl -s -w '\n[HTTP %{http_code}]\n' -d 'action=save&id=&title=Refactoring&author=Martin Fowler&genre=Non-Fiction&year=1999&in_stock=1' http://localhost:7013/bookapi
echo "=== validation (empty title) ==="
curl -s -w '\n[HTTP %{http_code}]\n' -d 'action=save&id=&title=&author=x&genre=Fiction&year=2000' http://localhost:7013/bookapi
echo "=== list after create ==="
curl -s "http://localhost:7013/bookapi?action=list" | grep -c 'Refactoring'
echo "=== get id=5 ==="
curl -s "http://localhost:7013/bookapi?action=get&id=5"
echo
echo "=== delete id=5 ==="
curl -s -d 'action=delete&id=5' http://localhost:7013/bookapi
echo
echo "=== list after delete (Refactoring count, expect 0) ==="
curl -s "http://localhost:7013/bookapi?action=list" | grep -c 'Refactoring'
