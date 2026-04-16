using FMC.Application.Interfaces;
using FMC.Infrastructure.Authentication;

namespace FMC.Infrastructure.Services;

public class EmailTemplateService : IEmailTemplateService
{
    private string GetHeader() => $@"
        <div style=""text-align: center; padding-bottom: 30px; border-bottom: 2px solid #f0f0f0;"">
            <img src=""cid:nlklogo"" alt=""Nationlink Dashboard"" width=""180"" style=""max-width: 180px; height: auto; display: block; margin: 0 auto;"" />
        </div>";

    private string GetFooter() => $@"
        <div style=""border-top:1px solid #eeeeee;padding-top:20px;text-align:center;"">
            <p style=""color:#b2bec3;font-size:12px;margin:0;"">
                This is an automated workflow notification.<br>
                © {DateTime.UtcNow.Year} Nationlink Finance Management Console. All rights reserved.
            </p>
        </div>";

    private string GetContainer(string content) => $@"
        <div style=""font-family:'Segoe UI', Roboto, Helvetica, Arial, sans-serif;max-width:600px;margin:20px auto;background:#ffffff;padding:40px;border-radius:12px;box-shadow:0 8px 30px rgba(0,0,0,0.04);border:1px solid #eaeaea;"">
            {GetHeader()}
            {content}
            {GetFooter()}
        </div>";

    public string GeneratePendingApprovalEmail(string orgName, string makerName, string targetCardholder, string maskedCardNumber, decimal amount)
    {
        var content = $@"
            <h2 style=""color:#ff9f43;margin-top:30px;font-size:24px;font-weight:800;letter-spacing:-0.5px;text-align:center;"">Pending Validation Request</h2>
            <p style=""color:#2d3436;font-size:15px;line-height:1.6;margin-bottom:24px;text-align:center;"">
                A new subscriber allotment request has been initiated by <strong>{makerName}</strong> and requires your validation to proceed.
            </p>
            
            <div style=""background:#f8f9fa;border-radius:8px;padding:24px;margin-bottom:24px;"">
                <h4 style=""margin:0 0 16px 0;color:#2d3436;font-size:14px;text-transform:uppercase;letter-spacing:1px;"">Request Details</h4>
                <table style=""width:100%;border-collapse:collapse;"">
                    <tr style=""border-bottom: 1px solid #e1e5ea;"">
                        <td style=""padding:12px 0;color:#636e72;font-size:14px;"">Target Cardholder</td>
                        <td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{targetCardholder}</td>
                    </tr>
                    <tr style=""border-bottom: 1px solid #e1e5ea;"">
                        <td style=""padding:12px 0;color:#636e72;font-size:14px;"">Service Card Number</td>
                        <td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{maskedCardNumber}</td>
                    </tr>
                    <tr>
                        <td style=""padding:12px 0;color:#636e72;font-size:14px;"">Transaction Amount</td>
                        <td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{amount:C}</td>
                    </tr>
                </table>
            </div>

            <p style=""color:#636e72;font-size:14px;line-height:1.5;margin-bottom:30px;text-align:center;"">
                Please log in to the Intelligence Oversight panel of your Finance Management Console to review and approve this transaction.
            </p>";

        return GetContainer(content);
    }

    public string GenerateTransactionApprovedEmail(string orgName, string targetCardholder, string maskedCardNumber, decimal amount)
    {
        var content = $@"
            <h2 style=""color:#00b894;margin-top:30px;font-size:24px;font-weight:800;letter-spacing:-0.5px;text-align:center;"">Transaction Approved</h2>
            <p style=""color:#2d3436;font-size:15px;line-height:1.6;margin-bottom:24px;text-align:center;"">
                Good news! A subscriber allotment request has been successfully validated and completed for <strong>{orgName}</strong>.
            </p>
            <div style=""background:#f8f9fa;border-radius:8px;padding:24px;margin-bottom:24px;"">
                <h4 style=""margin:0 0 16px 0;color:#2d3436;font-size:14px;text-transform:uppercase;letter-spacing:1px;"">Transaction Details</h4>
                <table style=""width:100%;border-collapse:collapse;"">
                    <tr style=""border-bottom: 1px solid #e1e5ea;"">
                        <td style=""padding:12px 0;color:#636e72;font-size:14px;"">Recipient Cardholder</td>
                        <td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{targetCardholder}</td>
                    </tr>
                    <tr style=""border-bottom: 1px solid #e1e5ea;"">
                        <td style=""padding:12px 0;color:#636e72;font-size:14px;"">Service Card Number</td>
                        <td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{maskedCardNumber}</td>
                    </tr>
                    <tr>
                        <td style=""padding:12px 0;color:#636e72;font-size:14px;"">Approved Amount</td>
                        <td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{amount:C}</td>
                    </tr>
                </table>
            </div>
            <p style=""color:#636e72;font-size:14px;line-height:1.5;margin-bottom:30px;text-align:center;"">The funds have been successfully settled to the subscriber account.</p>";

        return GetContainer(content);
    }

    public string GenerateWalletAdjustmentEmail(string orgName, string maskedCardNumber, decimal amount, decimal balance, bool isCredit)
    {
        var themeColor = isCredit ? "#4834d4" : "#eb4d4b";
        var actionTitle = isCredit ? "Wallet Credited Successfully" : "Wallet Adjustment (Debit)";
        var actionDesc = isCredit 
            ? $"funds have been successfully credited to <strong>{orgName}</strong> by the System Administrator."
            : $"a debit adjustment has been applied to the <strong>{orgName}</strong> organizational wallet by the System Administrator.";

        var content = $@"
            <h2 style=""color:{themeColor};margin-top:30px;font-size:24px;font-weight:800;letter-spacing:-0.5px;text-align:center;"">{actionTitle}</h2>
            <p style=""color:#2d3436;font-size:15px;line-height:1.6;margin-bottom:24px;text-align:center;"">
                This is an automated advisory confirming that {actionDesc}
            </p>
            
            <div style=""background:#f8f9fa;border-radius:8px;padding:24px;margin-bottom:24px;"">
                <h4 style=""margin:0 0 16px 0;color:#2d3436;font-size:14px;text-transform:uppercase;letter-spacing:1px;"">Transaction Details</h4>
                <table style=""width:100%;border-collapse:collapse;"">
                    <tr style=""border-bottom: 1px solid #e1e5ea;"">
                        <td style=""padding:12px 0;color:#636e72;font-size:14px;"">Organization Name</td>
                        <td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{orgName}</td>
                    </tr>
                    <tr style=""border-bottom: 1px solid #e1e5ea;"">
                        <td style=""padding:12px 0;color:#636e72;font-size:14px;"">Organization Card Number</td>
                        <td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{maskedCardNumber}</td>
                    </tr>
                    <tr style=""border-bottom: 1px solid #e1e5ea;"">
                        <td style=""padding:12px 0;color:#636e72;font-size:14px;"">Adjustment Amount</td>
                        <td style=""padding:12px 0;font-weight:800;color:{themeColor};text-align:right;font-size:18px;"">{Math.Abs(amount):C}</td>
                    </tr>
                    <tr>
                        <td style=""padding:12px 0;color:#636e72;font-size:14px;"">New Operational Balance</td>
                        <td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{balance:C}</td>
                    </tr>
                </table>
            </div>

            <p style=""color:#636e72;font-size:14px;line-height:1.5;margin-bottom:30px;text-align:center;"">
                These funds are available for dispersal to your organization's cardholders and subscribers.
            </p>";

        return GetContainer(content);
    }

    public string GenerateCapacityThresholdEmail(string orgName, decimal total, decimal dispersed, decimal pct, decimal remaining)
    {
        var alertType = pct >= 80m ? $"{pct:F0}% Operational Capacity Alert" : "Critical Liquidity Advisory";
        var content = $@"
            <h2 style=""color:#d63031;margin-top:30px;font-size:24px;font-weight:800;letter-spacing:-0.5px;text-align:center;"">{alertType}</h2>
            <p style=""color:#2d3436;font-size:15px;line-height:1.6;margin-bottom:24px;text-align:center;"">
                This is an automated advisory regarding the operational liquidity of <strong>{orgName}</strong>. Your tenant account has reached a structural capacity threshold and requires attention.
            </p>
            <div style=""background:#f8f9fa;border-radius:8px;padding:24px;margin-bottom:24px;"">
                <h4 style=""margin:0 0 16px 0;color:#2d3436;font-size:14px;text-transform:uppercase;letter-spacing:1px;"">Account Overview</h4>
                <table style=""width:100%;border-collapse:collapse;"">
                    <tr style=""border-bottom: 1px solid #e1e5ea;""><td style=""padding:12px 0;color:#636e72;font-size:14px;"">Total Institutional Wallet</td><td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{total:C}</td></tr>
                    <tr style=""border-bottom: 1px solid #e1e5ea;""><td style=""padding:12px 0;color:#636e72;font-size:14px;"">Volume Dispersed to Subscribers</td><td style=""padding:12px 0;font-weight:700;color:#2d3436;text-align:right;"">{dispersed:C} ({pct:F1}%)</td></tr>
                    <tr><td style=""padding:12px 0;color:#d63031;font-size:14px;font-weight:600;"">Remaining Organizational Capital</td><td style=""padding:12px 0;font-weight:800;color:#d63031;text-align:right;font-size:16px;"">{remaining:C}</td></tr>
                </table>
            </div>
            <p style=""color:#636e72;font-size:14px;line-height:1.5;margin-bottom:30px;text-align:center;"">We strongly advise replenishing your institutional reserve to ensure continuous functionality.</p>";

        return GetContainer(content);
    }
}
