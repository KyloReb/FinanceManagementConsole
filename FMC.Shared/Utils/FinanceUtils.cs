namespace FMC.Shared.Utils;

public static class FinanceUtils
{
    /// <summary>
    /// Masks a card or account number, showing only the first 6 and last 4 digits.
    /// Format: XXXXXX******XXXX
    /// </summary>
    /// <param name="cardNumber">The full account or card number.</param>
    /// <returns>A masked string or a default mask if input is null/short.</returns>
    public static string MaskCard(string? cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber)) return "****************";
        
        // If the card number is too short to mask (e.g. 10 chars or less), 
        // return it as is or handle gracefully.
        if (cardNumber.Length <= 10) return cardNumber;

        var firstSix = cardNumber.Substring(0, 6);
        var lastFour = cardNumber.Substring(cardNumber.Length - 4);
        var maskLength = cardNumber.Length - 10;
        
        return $"{firstSix}{new string('*', maskLength)}{lastFour}";
    }
}
