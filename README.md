# TechagroSyncServices

> 💼 **Commercial Project** — part of a private or client-facing initiative.

## Overview

**TechagroSyncServices** is a set of Windows Services that integrate with multiple supplier APIs to automatically fetch data and insert it into a database.  
Each service is independently responsible for processing a different supplier’s API and is fully logged using **Serilog**.

## Services

- **TechagroSyncServices.Gaska**

  - Fetches data from the **Gaska API** (JSON format)
  - Inserts parsed records into the database

- **TechagroSyncServices.Agroland**

  - Fetches data from the **Agroland API** (XML format)
  - Transforms and stores results in the database

- **TechagroSyncServices.Amaparts**
  - Fetches data from the **Ama-parts API** (CSV format)
  - Cleans and inserts data into the database

## Features

- Automated supplier data synchronization
- Format-specific parsing (JSON, XML, CSV)
- Structured logging with **Serilog**
- Reliable database integration

## Technologies Used

- **Frameworks:** .NET Framework
- **Languages:** C#
- **Data Sources:** JSON, XML, CSV APIs
- **Database:** SQL Server
- **Logging:** Serilog

## License

This project is proprietary and confidential. See the [LICENSE](LICENSE) file for more information.

---

© 2025-present [calKU0](https://github.com/calKU0)
