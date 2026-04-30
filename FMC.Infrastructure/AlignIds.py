import pyodbc

conn_str = (
    r'Driver={ODBC Driver 17 for SQL Server};'
    r'Server=(localdb)\mssqllocaldb;'
    r'Database=FMC;'
    r'Trusted_Connection=yes;'
)

try:
    conn = pyodbc.connect(conn_str)
    cursor = conn.cursor()
    
    print("Aligning Accounts TenantId with new Cardholder IDs...")
    cursor.execute("""
        UPDATE a
        SET a.TenantId = CAST(c.Id AS NVARCHAR(450))
        FROM Accounts a
        INNER JOIN Cardholders c ON a.TenantId = c.IdentityUserId
        WHERE c.IdentityUserId IS NOT NULL
    """)
    print(f"Updated {cursor.rowcount} accounts.")
    
    print("Aligning Transactions TenantId with new Cardholder IDs...")
    cursor.execute("""
        UPDATE t
        SET t.TenantId = CAST(c.Id AS NVARCHAR(450))
        FROM Transactions t
        INNER JOIN Cardholders c ON t.TenantId = c.IdentityUserId
        WHERE c.IdentityUserId IS NOT NULL
    """)
    print(f"Updated {cursor.rowcount} transactions.")
    
    conn.commit()
    print("Success.")
except Exception as e:
    print(f"Error: {e}")
finally:
    if 'conn' in locals():
        conn.close()
