# BitSmith: A Full-Stack Online Coding Platform

> A professional, coding practice platform built with a production-grade architecture.

---

### 🧠 Tech Stack

![.NET](https://img.shields.io/badge/.NET-8-blueviolet)
![C#](https://img.shields.io/badge/Language-C%23-purple)
![SQL Server](https://img.shields.io/badge/Database-SQL%20Server-red)
![Entity Framework Core](https://img.shields.io/badge/ORM-EF%20Core-orange)
![JWT](https://img.shields.io/badge/Auth-JWT-green)
![Swagger](https://img.shields.io/badge/API-Swagger-brightgreen)
![Angular](https://img.shields.io/badge/Frontend-Angular-dd0031)
---


### 🧩 System Architecture
```
┌────────────────────────────┐
│        Frontend UI         │
│      (Angular Client)      │
└──────────────┬─────────────┘
               │ REST API Calls (HTTPS)
               ▼
┌────────────────────────────┐
│      ASP.NET Core API      │
│   Controllers / Services   │
│  Auth / Rate Limiting /    │
│ Exception Middleware       │
└──────────────┬─────────────┘
               │ EF Core
               ▼
┌────────────────────────────┐
│      SQL Server DB         │
│ Migrations / Constraints   │
│  User / Problem / Submits  │
└────────────────────────────┘

Async Future Microservice:
┌────────────────────────────┐
│  Code Judge Engine (TODO)  │
│ Docker Sandbox Execution   │
└────────────────────────────┘
```

### 📂 Folder Structure
```
BitSmith/
└── dotnetBitSmith/
      ├── Controllers/
      │   ├── AuthController.cs
      │   ├── ProblemController.cs
      │   └── SubmissionController.cs
      ├── Data/
      │   ├── ApplicationDbContext.cs
      │   └── Migrations/
      ├── Entities/
      │   ├── User.cs, Problem.cs, Submission.cs, ... (10 total)
      ├── Exceptions/
      │   ├── DuplicateUserException.cs, NotFoundException.cs, ...
      ├── Interfaces/
      │   ├── IAuthService.cs
      │   ├── IProblemService.cs
      │   └── ISubmissionService.cs
      ├── Middleware/
      │   └── ExceptionHandlingMiddleware.cs
      ├── Models/
      │   ├── Auth/ (DTOs)
      │   ├── Problems/ (DTOs)
      │   └── Submissions/ (DTOs)
      ├── Services/
      │   ├── AuthService.cs
      │   ├── ProblemService.cs
      │   └── SubmissionService.cs
      ├── Properties/
      │   └── launchSettings.json
      ├── appsettings.Development.json (Git Ignored)
      ├── appsettings.json
      └── Program.cs

```
> Clean, modular structure following SOLID and Clean Architecture principles.

## 📋 Table of Contents
- [About The Project](#about-the-project)
- [✨ Key Features](#-key-features)
- [🚀 Implemented API Endpoints](#-implemented-api-endpoints)
- [🛡️ Architectural & Security Highlights](#-architectural--security-highlights)
- [🛠️ Getting Started](#-getting-started)
  - [Prerequisites](#prerequisites)
  - [Installation & Setup](#installation--setup)
- [🗺️ Roadmap](#-roadmap)

---

## 🎯 About The Project

BitSmith is a comprehensive online coding platform similar to LeetCode.  
The backend REST API (**dotnetBitSmith**) is fully functional for core features.  

Focus so far: **Security**, **Performance**, **Clean Architecture**, **Scalability**

### **Primary Tech Stack**
- **Backend:** ASP.NET Core 8
- **Database:** SQL Server
- **ORM:** Entity Framework Core
- **Authentication:** JWT + BCrypt
- **Architecture:** Clean Architecture (SOLID + DRY)

---

## ✨ Key Features

- ✅ Secure JWT Authentication (BCrypt hashing)
- ✅ Role-Based Access Control (RBAC)
- ✅ API Rate Limiting
- ✅ EF Core Transactions for atomic operations
- ✅ Fully layered architecture — Controllers / Services / Repos / DTOs
- ✅ Secrets outside source control
- ✅ Detailed exception handling middleware

---

## 🚀 Implemented API Endpoints

### **Authentication (`/api/auth`)**
| Method | Endpoint | Access | Description |
|--------|---------|--------|-------------|
| POST | `/api/auth/register` | Public | Register new user (hash + validate) |
| POST | `/api/auth/login` | Public | Login and get JWT token |

### **Problems (`/api/problem`)**
| Method | Endpoint | Access | Description |
|--------|---------|--------|-------------|
| GET | `/api/problem` | Public | Get all problem summaries |
| GET | `/api/problem/{id}` | Public | Get detailed problem info |
| POST | `/api/problem` | Admin Only | Create a new problem |

### **Submissions (`/api/submission`)**
| Method | Endpoint | Access | Description |
|--------|---------|--------|-------------|
| POST | `/api/submission` | Authenticated User | Submit code (status: Pending) |

---

## 🛡️ Architectural & Security Highlights

- **Global Exception Handling Middleware**
- **JWT w/ Secure Secret Key Storage**
- **BCrypt Password Hashing**
- **Rate Limiting on login & submissions**
- **EF Core migrations with complete schema**
- **Efficient LINQ projections (`.Select()`)**
- **Transactional DB operations for Admin actions**

---

## 🛠️ Getting Started

### Prerequisites
- ✅ .NET 8 SDK  
- ✅ SQL Server / LocalDB  
- ✅ JWT Secret Key  

---

### Installation & Setup

```bash
git clone https://github.com/YOUR_USERNAME/BitSmith.git
cd BitSmith/dotnetBitSmith
```
Create appsettings.Development.json inside dotnetBitSmith/:
```
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=BitSmithDb;Trusted_Connection=True;Encrypt=False;"
  },
  "JwtSettings": {
    "Key": "YOUR_SUPER_SECRET_32_CHARACTER_PLUS_KEY_GOES_HERE"
  }
}
```


Create database:
```
dotnet ef database update
```

Run API:
```
dotnet run
```

Swagger UI will be available at:
```
https://localhost:5078/swagger
```
## 🗺️ Roadmap
- [ ] Add read endpoints for submissions (`get my submissions`)
- [ ] Build Angular frontend (`angularBitSmith`)
- [ ] Implement judging engine (`ICompilationService`)

## ⭐ Future Enhancements
- Docker sandboxed judge system
- Leaderboards & performance analytics
- Community discussions / forums
- Problem difficulty ratings
- Admin dashboard UI
