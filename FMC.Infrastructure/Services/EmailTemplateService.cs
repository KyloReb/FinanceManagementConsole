using FMC.Application.Interfaces;
using FMC.Infrastructure.Authentication;
using FMC.Shared.Utils;

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
            
            <div style=""background:#f8f9fa;border-radius:12px;padding:24px;margin-bottom:24px;border:1px solid #e1e5ea;"">
                <h4 style=""margin:0 0 16px 0;color:#2d3436;font-size:12px;text-transform:uppercase;letter-spacing:1.5px;font-weight:800;"">Request Details</h4>
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
                        <td style=""padding:16px 0 0 0;color:#2d3436;font-size:15px;font-weight:700;"">Transaction Amount</td>
                        <td style=""padding:16px 0 0 0;font-weight:900;color:#ff9f43;text-align:right;font-size:22px;"">{amount:C}</td>
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
            <div style=""background:#f8f9fa;border-radius:12px;padding:24px;margin-bottom:24px;border:1px solid #e1e5ea;"">
                <h4 style=""margin:0 0 16px 0;color:#2d3436;font-size:12px;text-transform:uppercase;letter-spacing:1.5px;font-weight:800;"">Transaction Details</h4>
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
                        <td style=""padding:16px 0 0 0;color:#2d3436;font-size:15px;font-weight:700;"">Settled Amount</td>
                        <td style=""padding:16px 0 0 0;font-weight:900;color:#00b894;text-align:right;font-size:22px;"">{amount:C}</td>
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
                        <td style=""padding:12px 0;font-weight:900;color:{themeColor};text-align:right;font-size:22px;"">{Math.Abs(amount):C}</td>
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

    public string GenerateBatchNotificationEmail(string orgName, string batchAction, IEnumerable<FMC.Shared.DTOs.TransactionDto> transactions, bool hasAttachments)
    {
        var count = transactions.Count();
        var totalAmount = transactions.Sum(t => t.Amount);
        var previewItems = transactions.Take(20).ToList();
        var isApproved = batchAction.Equals("Approved", StringComparison.OrdinalIgnoreCase);
        var themeColor = isApproved ? "#00b894" : "#4834d4";

        var tableRows = "";
        foreach (var item in previewItems)
        {
            tableRows += $@"
                <tr style=""border-bottom: 1px solid #f1f2f6;"">
                    <td style=""padding:12px 8px;color:#2d3436;font-size:13px;font-weight:600;"">{item.Subscriber}</td>
                    <td style=""padding:12px 8px;color:#636e72;font-size:12px;text-align:center;font-family:monospace;"">{FinanceUtils.MaskCard(item.AccountNumber ?? "")}</td>
                    <td style=""padding:12px 8px;font-weight:700;color:#2d3436;text-align:right;font-size:13px;"">{item.Amount:N2}</td>
                </tr>";
        }

        var attachmentNotice = hasAttachments 
            ? $@"<div style=""margin-top:20px;padding:16px;background:rgba(72, 52, 212, 0.05);border-radius:12px;border:1px solid rgba(72, 52, 212, 0.1);"">
                    <p style=""margin:0;color:#4834d4;font-size:13px;line-height:1.5;"">
                        <strong>Volume Notice:</strong> This batch contains {count} records. A full reconciliation manifest (Excel) is attached for your records.
                    </p>
                 </div>" 
            : "";

        var content = $@"
            <div style=""text-align:center;margin-top:30px;"">
                <span style=""background:{themeColor}10;color:{themeColor};padding:6px 14px;border-radius:20px;font-size:11px;font-weight:900;text-transform:uppercase;letter-spacing:1px;"">Batch {batchAction}</span>
                <h2 style=""color:#2d3436;margin-top:16px;font-size:28px;font-weight:900;letter-spacing:-1px;"">Settlement Consolidated</h2>
                <p style=""color:#636e72;font-size:15px;line-height:1.6;max-width:400px;margin:10px auto 30px auto;"">
                    A batch of <strong>{count}</strong> transactions has been <strong>{batchAction.ToLower()}</strong> and settled for {orgName}.
                </p>
            </div>

            <div style=""background:#ffffff;border:1px solid #f1f2f6;border-radius:16px;padding:24px;box-shadow:0 10px 30px rgba(0,0,0,0.02);"">
                <div style=""margin-bottom:20px;border-bottom:1px solid #f1f2f6;padding-bottom:15px;"">
                    <table style=""width:100%;border-collapse:collapse;"">
                        <tr>
                            <td style=""vertical-align:top;"">
                                <p style=""margin:0;font-size:11px;color:#b2bec3;text-transform:uppercase;font-weight:800;letter-spacing:1px;"">Batch Liquidity</p>
                                <p style=""margin:5px 0 0 0;font-size:24px;font-weight:900;color:{themeColor};"">₱{totalAmount:N2}</p>
                            </td>
                            <td style=""vertical-align:top;text-align:right;"">
                                <p style=""margin:0;font-size:11px;color:#b2bec3;text-transform:uppercase;font-weight:800;letter-spacing:1px;"">Item Count</p>
                                <p style=""margin:5px 0 0 0;font-size:24px;font-weight:900;color:#2d3436;"">{count}</p>
                            </td>
                        </tr>
                    </table>
                </div>

                <h4 style=""margin:20px 0 12px 0;color:#2d3436;font-size:12px;text-transform:uppercase;letter-spacing:1px;font-weight:800;"">Transaction Breakdown</h4>
                <table style=""width:100%;border-collapse:collapse;"">
                    <thead>
                        <tr style=""border-bottom: 2px solid #f1f2f6;"">
                            <th style=""text-align:left;padding:8px;font-size:11px;color:#b2bec3;text-transform:uppercase;"">Subscriber</th>
                            <th style=""text-align:center;padding:8px;font-size:11px;color:#b2bec3;text-transform:uppercase;"">Card Number</th>
                            <th style=""text-align:right;padding:8px;font-size:11px;color:#b2bec3;text-transform:uppercase;"">Amount</th>
                        </tr>
                    </thead>
                    <tbody>
                        {tableRows}
                    </tbody>
                </table>
                {attachmentNotice}
            </div>";

        return GetContainer(content);
    }

    public string GenerateBulkUploadNotificationEmail(string orgName, string makerName, int totalCount, decimal totalAmount, bool isCredit, List<FMC.Shared.DTOs.BulkTransactionRowDto>? sampleRows = null)
    {
        var actionType = isCredit ? "Credit" : "Debit";
        var themeColor = "#4834d4";
        
        var tableRows = "";
        if (sampleRows != null && sampleRows.Any())
        {
            foreach (var row in sampleRows)
            {
                tableRows += $@"
                    <tr style=""border-bottom: 1px solid #f1f2f6;"">
                        <td style=""padding:12px 8px;color:#2d3436;font-size:13px;font-weight:600;"">{row.Subscriber}</td>
                        <td style=""padding:12px 8px;color:#636e72;font-size:12px;text-align:center;font-family:monospace;"">{FinanceUtils.MaskCard(row.CardNumber)}</td>
                        <td style=""padding:12px 8px;font-weight:700;color:#2d3436;text-align:right;font-size:13px;"">{row.Amount:N2}</td>
                    </tr>";
            }
        }

        var sampleTable = string.IsNullOrEmpty(tableRows) ? "" : $@"
            <h4 style=""margin:24px 0 12px 0;color:#2d3436;font-size:12px;text-transform:uppercase;letter-spacing:1px;font-weight:800;"">Batch Preview (First {sampleRows?.Count})</h4>
            <table style=""width:100%;border-collapse:collapse;"">
                <thead>
                    <tr style=""border-bottom: 2px solid #f1f2f6;"">
                        <th style=""text-align:left;padding:8px;font-size:11px;color:#b2bec3;text-transform:uppercase;"">Cardholder</th>
                        <th style=""text-align:center;padding:8px;font-size:11px;color:#b2bec3;text-transform:uppercase;"">Card Number</th>
                        <th style=""text-align:right;padding:8px;font-size:11px;color:#b2bec3;text-transform:uppercase;"">Amount</th>
                    </tr>
                </thead>
                <tbody>
                    {tableRows}
                </tbody>
            </table>";

        var content = $@"
            <div style=""text-align:center;margin-top:30px;"">
                <span style=""background:{themeColor}10;color:{themeColor};padding:6px 14px;border-radius:20px;font-size:11px;font-weight:900;text-transform:uppercase;letter-spacing:1px;"">Awaiting Validation</span>
                <h2 style=""color:#2d3436;margin-top:16px;font-size:28px;font-weight:900;letter-spacing:-1px;"">Bulk Batch Submission</h2>
                <p style=""color:#636e72;font-size:15px;line-height:1.6;max-width:400px;margin:10px auto 30px auto;"">
                    A new bulk <strong>{actionType}</strong> batch has been initiated by <strong>{makerName}</strong> for {orgName}.
                </p>
            </div>

            <div style=""background:#ffffff;border:1px solid #f1f2f6;border-radius:16px;padding:24px;box-shadow:0 10px 30px rgba(0,0,0,0.02);"">
                <div style=""margin-bottom:20px;border-bottom:1px solid #f1f2f6;padding-bottom:15px;"">
                    <table style=""width:100%;border-collapse:collapse;"">
                        <tr>
                            <td style=""vertical-align:top;"">
                                <p style=""margin:0;font-size:11px;color:#b2bec3;text-transform:uppercase;font-weight:800;letter-spacing:1px;"">Total Batch Value</p>
                                <p style=""margin:5px 0 0 0;font-size:24px;font-weight:900;color:{themeColor};"">₱{totalAmount:N2}</p>
                            </td>
                            <td style=""vertical-align:top;text-align:right;"">
                                <p style=""margin:0;font-size:11px;color:#b2bec3;text-transform:uppercase;font-weight:800;letter-spacing:1px;"">Item Count</p>
                                <p style=""margin:5px 0 0 0;font-size:24px;font-weight:900;color:#2d3436;"">{totalCount}</p>
                            </td>
                        </tr>
                    </table>
                </div>

                {sampleTable}

                <div style=""margin-top:30px;padding:20px;background:#f8f9fa;border-radius:12px;text-align:center;"">
                    <p style=""margin:0;color:#636e72;font-size:14px;line-height:1.5;"">
                        Please log in to the <strong>Intelligence Oversight</strong> panel of your console to authorize this settlement.
                    </p>
                </div>
            </div>";

        return GetContainer(content);
    }
}
