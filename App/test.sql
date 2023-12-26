SELECT Time.Year, Time.Month, Time.Day, Time.Hour, Time.Minute, ReportVariableData.VariableValue
FROM Time
JOIN ReportVariableData ON Time.TimeIndex = ReportVariableData.TimeIndex
WHERE (DayType = "Sunday" or DayType = "Monday" or DayType = "Tuesday"
or DayType = "Wednesday" or DayType = "Thursday" or DayType = "Friday"
or DayType = "Saturday")
and ReportVariableData.ReportVariableDataDictionaryIndex = 23 ;
