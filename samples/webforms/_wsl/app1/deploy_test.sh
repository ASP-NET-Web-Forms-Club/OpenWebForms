#!/bin/bash
SRC='/mnt/d/Claude Files/OpenWebForms/samples/webforms'
pkill -f 'SampleHost.dll 7011' 2>/dev/null
sleep 1
rm -f ~/owf/app1/wwwroot/*
cp "$SRC/_wsl/app1/Default.aspx" ~/owf/app1/wwwroot/
cp "$SRC/_shared/site.css" ~/owf/app1/wwwroot/
cd ~/owf/app1
(nohup dotnet SampleHost.dll 7011 > ~/owf/app1.log 2>&1 &)
sleep 4
curl -s -o /tmp/get1.html http://localhost:7011/Default.aspx
curl -s -o /tmp/get1.html -w 'GET  HTTP %{http_code} bytes=%{size_download}\n' http://localhost:7011/Default.aspx
echo "render markers:"
grep -o 'The Hobbit\|badge-yes\|badge-no\|__VIEWSTATE\|RadioButtonList\|rblFormat\|cblTags\|lnkEdit\|Delete\|ImageButton\|ddlGenre' /tmp/get1.html | sort | uniq -c

VS=$(grep -oP 'name="__VIEWSTATE" id="__VIEWSTATE" value="\K[^"]*' /tmp/get1.html)
VG=$(grep -oP 'name="__VIEWSTATEGENERATOR" id="__VIEWSTATEGENERATOR" value="\K[^"]*' /tmp/get1.html)
EV=$(grep -oP 'name="__EVENTVALIDATION" id="__EVENTVALIDATION" value="\K[^"]*' /tmp/get1.html)
echo "scraped: VS=${#VS} chars, VG=$VG, EV=${#EV} chars"

curl -s -o /tmp/post1.html -w 'POST HTTP %{http_code} bytes=%{size_download}\n' \
  --data-urlencode "__VIEWSTATE=$VS" \
  --data-urlencode "__VIEWSTATEGENERATOR=$VG" \
  --data-urlencode "__EVENTVALIDATION=$EV" \
  --data-urlencode "txtTitle=Refactoring" \
  --data-urlencode "txtAuthor=Martin Fowler" \
  --data-urlencode "txtYear=1999" \
  --data-urlencode "ddlGenre=Non-Fiction" \
  --data-urlencode "rblFormat=Hardcover" \
  --data-urlencode "cblTags\$0=Bestseller" \
  --data-urlencode "btnSave=Save" \
  http://localhost:7011/Default.aspx
echo "operate result: 'Book added' count=$(grep -c 'Book added' /tmp/post1.html), 'Refactoring' rows=$(grep -c 'Refactoring' /tmp/post1.html)"
echo "log tail:"; tail -n 5 ~/owf/app1.log
