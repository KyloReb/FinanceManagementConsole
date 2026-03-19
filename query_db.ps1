$conn = New-Object System.Data.SqlClient.SqlConnection("Server=104.155.226.186;Database=FMC_App;User Id=sqlserver;Password=1234;TrustServerCertificate=True;Connection Timeout=30;Encrypt=False;")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "DELETE FROM AspNetUsers WHERE EmailConfirmed = 0"
$deleted = $cmd.ExecuteNonQuery()
Write-Output "Deleted rows: $deleted"
$conn.Close()
