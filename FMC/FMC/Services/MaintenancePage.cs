using System.Text;

namespace FMC.Services;

public static class MaintenancePage
{
    public const string LogoBase64 = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAKMAAAAxCAYAAABV5z9nAAAAAXNSR0IArs4c6QAAB5RJREFUeF7tnH1sFGUUxX/b3e22BRQFKfKjCIqiKEHxiyIxGjVGJcZErSExEAIhIfAPEkI0xPgP/hGMJpqYGI3GGGOs0RqNIPEXf1CDWqFVUClFKrV8lK92t93pvLftdrvdnZ3dnV0w6SSb3XnnnXPvM+89c9+9913F4/E4kiRAEiAJ5JkEFMqznpJ3SQIkgTwkgEQ5D18V6TJJoCgSINFKEv2/fxYFpu12k0QrSc30H4lWEmD+l0QriXP9T6KVJMD8L4lWEsH6n0QrSYD5XxKtJIL1P4lWEsH6n0QrSYD5XxKtJIL1P4lWkgDzvyRaSQTnfxKtJAHmf0m0kgjW/yRaSQLM/5JoJRGc/0m0kgSY/yXRSiI4/0uikQSY/yXRSiI4/0+3aM1beqF6x21Y3X4faltjKl6P5uGTxq4xR91z7Gg4y78S7d+UjqHqnagKGtcOdS3Zf5JoAe9R7mh7p+o4N+x6tg+Xh95E54CHkwc8/G/tXQN27lTH75Tv7DtJNAfu64Uptb6iGjZ2u2t7/PH7A+x4eQBrO7qnk3Fh9X2DKBhHYyr7xQHm0z+3aOPxeEF9Gq/O3MK2rTGVyF3b1Y1t285l3WfPovVoJQl7U/E5QzA0GqXKyspJLBaL6XtBEKanp5GIIhKJYGxsDCNjVzC+dQyV4RBut1cjo/AzW/3CpfNYt+5cQrT3b8KFQR+6BmMwvL7s2sTMkWhzJdjcbt+IiorKCd1u1zY2NobGxka0tLRA0zS0trbi7NmzUFUV9fX1qKmpgWEYuHr1Kjo6OtDc3Iza2lqMjo6ira0NoVAIS5cuRW9vLzo7O9HY2Ija2lqM31N4sPkoXv3sL0xP/4YbbpC/j4yMwPMHIEINhkRVVZUOzx8g4r06CAu+Z80fM2PWrFmpW/R0dHRMJCKJ1PO7rKwM6L8PjXQeBky0AFhS5mEDE0Mza4AnYg6orwf6R4Br/R5YZQ1BNL8N8j39ov2nQJ/YiwhjQBo1TAyRYFwkPEe0xQww7eR4L1nhpxKtzI06Z8sqxGJ3VAyYByZ+DGh/YqLTKFLxvf4TYOBntNH82v2POHz4MDZt2oS2tjb09PRgz549aG5uRkNDA4aGhrB3717U1dWhra0NIyMj+Pjjj7FkyRLU19dj7969qK+vR0tLCwCgdHIEpct/waH2m2htNar6evXq8Pnnn2tQFHQfOYL1n36K4NuBsYkGNL7bGHP7fAW/3/eG2+1+UFEUfPrpp6irq8PSpUvR29uL3bt3Y+nSpXj22WcxPDyMAwcOYPXq1Vi6dClGRkZw4MAB1NXVob29Ha2trVixYgXmz5+PK1euYOfOnaiurkYAuxAKa1i8eLGxeze0q+3Ff0pck8gka25ujoXD4TxwFlN4PB7Mnj17wLhxv1m3bh22bt2K2tpaDA4OYvv27airq8PGjRsxOjqKzz//HLW1tWhubobf78fmzZvh9XqxadMmeL1evPDCC+ju7sb27dtRX1+PDRs2YGJiAtu2bUNNTQ2eW7UI27dswSeffGIE3QrQ1bkWixZ9IG3+bCmP2O12G7/8qSlfzpo1a9xg5A0rV67MFqAvL7Nv2LABGzduhNfrRTAYxDvvvIO6ujo0NTUBAFpaWlBTU4NNmzYhEAjg5Zdfhs/nw+bNm+H1evHyyy+ju7sbmzdvRn19PTZs2IC+vj5Mnz4dTU1NePLJJ5GgjURj2LNnD5qbm9HS0gJN06BpGkpKSrB27YtobGzEoUOH8NVXX8Hr9eLJJ5+EaRoHQH3+fKMsFNjS0hJ2OBzD2fSsWEWrajpmzpw54na7uw4cOJCXgl2/fj02bdoEj8eDYDCI3bt3o6amBk1NTRgbG0NLSwu8Xi+ee+45BAIBfPzxx/B6vdi0aZNx+frrr+P06dPYunUr6uvrUV9fj3A4jIaGBvh8PjzxxBMJGo1oDgQCqK2tNcK9dgei0SgeeWQVNm7ciC+//BKffPIJfD6f0VYDxPzly5fitm0v4euvD+Po0Z8wffp0PP7440Y2cwCbN2+G3+/HK6+8gpqaGjzxxBMYHh7GgQMHsHz5ctTX12N4eBjt7e1Yvnw5GhsbYRhGDmi/3w+/34/HHnsMoyOjWLJkCV566SX8/PNP+Pzzz+Hz+bBx48YEjZEpSUnJyMqVK1+fiqKdO3cO7r33XiQlJjh/3n4MDw9jYGAAiqLgkUcewfj4OHp7exGLxbB06VKEQiEMDAxg+fLlKC0tRSgUQn9/P1avXp2IayYnJ3H48GEMDAygra0N8+bNw0svvYS+vj7s27cPoVAoQROJRGa3trZ+YFeQe/fuxaJFi4xOVP6Txn8XL17E/fd/NxXhPp3czp07kZQ0+Mknn5BoNWVH4E1KuF3/Z8xCmoqC12+xsN3KMFQ8wm/PBMjIQGYPIGjBhT+/TKGjowNr166dcn+npqbYJ598YkhF8Yw0SUmJEBsOJIh2Yhwkp8kEaTx4m2i0vxgOJJsX0yRarpG+JtqMPh3HgeRB+7tEC9Mc+XeXBKX2Fy0G07FSvpNoJYr43yVaSt7m/A6SaCVFHJeLaCWLuxwkWkkRx+UiWsniLgflMFqpxZ0N0Qr0Z9a+/5ABbiXa/D8J1v8kWkkE538SrSQB5n9JtJII1v8kWkkCzP+SaCURrP9JtJIE/j/+mbxrqdpfPAAAAABJRU5ErkJggg==";

    public static string FullLockHtml(string message)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<meta http-equiv=""refresh"" content=""30""><title>Under Maintenance | FMC</title>
<style>
* {{ margin:0; padding:0; box-sizing:border-box; }}
body {{ font-family:'Segoe UI',system-ui,-apple-system,sans-serif; display:flex; align-items:center; justify-content:center; min-height:100vh; transition:background 0.3s,color 0.3s; }}
@media (prefers-color-scheme: dark) {{ body {{ background:#0a0a0f; color:#e0e0e0; }} .accent {{ color:#b8860b; }} .footer {{ opacity:0.3; }} .contact {{ background:rgba(255,255,255,0.04); border-color:rgba(255,255,255,0.06); }} }}
@media (prefers-color-scheme: light) {{ body {{ background:#f1f5f9; color:#1e1e1e; }} .accent {{ color:#b8860b; }} .footer {{ opacity:0.5; }} .contact {{ background:rgba(0,0,0,0.03); border-color:rgba(0,0,0,0.06); }} }}
.container {{ text-align:center; max-width:460px; padding:40px 24px; }}
.logo {{ width:140px; height:auto; margin-bottom:28px; opacity:0.9; }}
h1 {{ font-size:26px; font-weight:900; letter-spacing:-0.03em; margin-bottom:10px; }}
p {{ font-size:14px; line-height:1.6; opacity:0.55; margin-bottom:6px; }}
.accent {{ font-weight:700; }}
.contact {{ margin-top:28px; padding:16px 20px; border-radius:14px; border:1px solid; font-size:12px; line-height:1.5; opacity:0.7; }}
.contact a {{ color:inherit; }}
.pulse {{ width:6px; height:6px; background:#b8860b; border-radius:50%; display:inline-block; margin-right:6px; animation:pulse 2s infinite; }}
@@keyframes pulse {{ 0%,100% {{ opacity:0.3; }} 50% {{ opacity:1; }} }}
.footer {{ margin-top:24px; font-size:11px; }}
.logout {{ margin-top:28px; display:inline-block; padding:14px 36px; border-radius:12px; border:2px solid #b8860b; font-size:14px; font-weight:700; text-decoration:none; color:#b8860b; cursor:pointer; transition:all 0.2s; letter-spacing:0.03em; }}
.logout:hover {{ background:#b8860b; color:#fff; }}
</style></head>
<body><div class=""container"">
<img src=""{LogoBase64}"" alt=""NLK Logo"" class=""logo"" />
<h1>Under Maintenance</h1>
<p>{System.Net.WebUtility.HtmlEncode(message)}</p>
<p class=""accent"">&#9679; We&rsquo;ll be back shortly</p>
<div class=""contact"">
    &#128231; For questions or inquiries, please contact<br/>
    <a href=""mailto:nl.admin@nationlink.ph"">nl.admin@nationlink.ph</a>
</div>
<div class=""footer""><span class=""pulse""></span>FMC &middot; {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</div>
<a class=""logout"" href=""/login""><strong>Sign Out</strong></a>
</div></body></html>";
    }

    public static string GraceHtml(int remainingSeconds, string message)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<meta http-equiv=""refresh"" content=""10""><title>Maintenance Notice | FMC</title>
<style>
* {{ margin:0; padding:0; box-sizing:border-box; }}
body {{ font-family:'Segoe UI',system-ui,-apple-system,sans-serif; display:flex; align-items:center; justify-content:center; min-height:100vh; transition:background 0.3s,color 0.3s; }}
@media (prefers-color-scheme: dark) {{ body {{ background:#0f0f13; color:#e0e0e0; }} .contact {{ background:rgba(255,255,255,0.04); border-color:rgba(255,255,255,0.06); }} }}
@media (prefers-color-scheme: light) {{ body {{ background:#f1f5f9; color:#1e1e1e; }} .contact {{ background:rgba(0,0,0,0.03); border-color:rgba(0,0,0,0.06); }} }}
.container {{ text-align:center; max-width:520px; padding:40px 24px; }}
.logo {{ width:140px; height:auto; margin-bottom:24px; opacity:0.9; }}
h1 {{ font-size:24px; font-weight:900; letter-spacing:-0.02em; margin-bottom:8px; }}
p {{ font-size:14px; line-height:1.6; opacity:0.55; margin-bottom:12px; }}
.countdown {{ font-size:48px; font-weight:900; color:#b8860b; font-family:'JetBrains Mono','Roboto Mono',monospace; margin:20px 0; letter-spacing:0.05em; }}
.label {{ font-size:10px; font-weight:800; text-transform:uppercase; letter-spacing:0.12em; opacity:0.3; }}
.contact {{ margin-top:24px; padding:14px 18px; border-radius:14px; border:1px solid; font-size:12px; line-height:1.5; opacity:0.65; }}
.contact a {{ color:inherit; }}
.footer {{ margin-top:24px; font-size:11px; opacity:0.25; }}
.logout {{ margin-top:28px; display:inline-block; padding:14px 36px; border-radius:12px; border:2px solid #b8860b; font-size:14px; font-weight:700; text-decoration:none; color:#b8860b; cursor:pointer; transition:all 0.2s; letter-spacing:0.03em; }}
.logout:hover {{ background:#b8860b; color:#fff; }}
</style></head>
<body><div class=""container"">
<img src=""{LogoBase64}"" alt=""NLK Logo"" class=""logo"" />
<h1>Scheduled Maintenance</h1>
<p>{System.Net.WebUtility.HtmlEncode(message)}</p>
<div class=""label"">Time remaining</div>
<div class=""countdown"" id=""cd"">{TimeSpan.FromSeconds(remainingSeconds):hh\:mm\:ss}</div>
<p>Please save your work. The system will be unavailable during this time.</p>
<div class=""contact"">
    &#128231; Questions? <a href=""mailto:nl.admin@nationlink.ph"">nl.admin@nationlink.ph</a>
</div>
<div class=""footer"">FMC &middot; Auto-refreshes every 10 seconds</div>
<a class=""logout"" href=""/login""><strong>Sign Out</strong></a>
<script>
(function(){{ var s={remainingSeconds};setInterval(function(){{s--;var h=Math.floor(s/3600),m=Math.floor((s%3600)/60),sec=s%60;document.getElementById('cd').textContent=
(h+'').padStart(2,'0')+':'+(m+'').padStart(2,'0')+':'+(sec+'').padStart(2,'0');if(s<=0)location.reload();}},1000);}})();
</script>
</div></body></html>";
    }
}
