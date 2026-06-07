#!/bin/bash
SRC='/mnt/d/Claude Files/OpenWebForms/samples/webforms'
pkill -f 'SampleHost.dll 7013' 2>/dev/null
sleep 1
rm -rf ~/owf/app3
cp -r ~/owf/_host ~/owf/app3
rm -f ~/owf/app3/wwwroot/*
cp "$SRC/_wsl/app3/Global.asax" ~/owf/app3/wwwroot/
cp "$SRC/_shared/site.css" ~/owf/app3/wwwroot/
cd ~/owf/app3
(nohup dotnet SampleHost.dll 7013 > ~/owf/app3.log 2>&1 &)
sleep 5
for u in "/" "/bookapi" "/nothing"; do
  printf 'GET %-12s ' "$u"
  curl -s -o /tmp/p.txt -w 'HTTP %{http_code} bytes=%{size_download} :: ' "http://localhost:7013$u"
  head -c 120 /tmp/p.txt; echo
done
echo "=== log tail ==="; tail -n 8 ~/owf/app3.log
