## 0.0.17 (2021/03/28)
* ADDED: Support for K4os.Quarterback

## 0.0.16 (2020/10/20)
* ADDED: DefaultJobHandler
* ADDED: NewScopeJobHandler
* FIXED: MediatR handler creates new resolution scopes
* FIXED: Potential TaskCancelledException on dispose

## 0.0.15 (2020/07/24)
* FIXED: MySQL driver compatible with 5.7 (was 8.x)

## 0.0.14 (2020/06/30)
* FIXED: Safer DB migrations

## 0.0.11 (2020/06/25)
* FIXED: Lowered requirement for MySqlConnector (to 0.56) 

## 0.0.9 (2020/06/24)
* Set fixed dependencies
* FIXED: removed 'set' methods from ISchedulerConfig interface
* ADDED: Install method AnySqlStorage

## 0.0.7 (2020/06/23)
* Modified default migration mechanism to avoid migration name conflicts
* DbJobSchedulerConfig is created with default values

## 0.0.3 (2020/06/18)
* MySQL, Sqlite, Postgres, SQL Server
