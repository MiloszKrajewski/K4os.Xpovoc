## 0.1.2 (2021/05/08)
* CHANGED: upgraded MySqlConnector to v1
* CHANGED: upgraded Dapper to v2
* CHANGED: updated minimum required versions of dependencies
* BUGFIX: removed PruneInterval setter from interface

## 0.0.20 (2021/04/27)
* CHANGED: keep completed and failed jobs (for a period of time)
* ADDED: pruning archived (completed and failed) jobs
* ADDED: added job status to scan index

## 0.0.18 (2021/03/29)
* CHANGED: changed index on jobs to use invisible_until (better performance)

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
