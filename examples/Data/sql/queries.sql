-- Run against an initialized L05 database. Amounts are INTEGER minor units.
SELECT CategoryName, COUNT(*) AS Count, SUM(AmountMinor) AS AmountMinor
FROM Expenses
GROUP BY CategoryName
HAVING SUM(AmountMinor) >= 2000
ORDER BY CategoryName;

-- LEFT JOIN retains categories without expenses; COUNT(e.Sequence) counts matched rows.
SELECT c.Name, COUNT(e.Sequence) AS Count, COALESCE(SUM(e.AmountMinor), 0) AS AmountMinor
FROM Categories AS c
LEFT JOIN Expenses AS e ON e.CategoryName = c.Name
GROUP BY c.Name
ORDER BY c.Name;

SELECT ExternalId FROM Expenses WHERE Note IS NULL ORDER BY Day, Sequence;

-- $day and $sequence are bound by the provider, never string-interpolated.
EXPLAIN QUERY PLAN
SELECT Sequence, ExternalId, Day, CategoryName, AmountMinor, Version
FROM Expenses
WHERE Day >= $day AND (Day > $day OR Sequence > $sequence)
ORDER BY Day, Sequence LIMIT 20;
