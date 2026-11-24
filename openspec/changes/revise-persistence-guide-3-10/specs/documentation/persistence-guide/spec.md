# Spec Delta

## Purpose

Define the behavior required for the persistence guide to teach reliable, version-pinned Elsa 3.10.2 persistence to code-first ASP.NET Core developers.

## ADDED Requirements

### Requirement: Accurate EF Core provider guidance
The guide SHALL identify the Elsa 3.10.2 EF Core provider packages for SQL Server, PostgreSQL, SQLite, and MySQL, explain version alignment, and state the persistence coverage of each package.

#### Scenario: Reader selects a relational provider
- **WHEN** a reader chooses a relational database for Elsa persistence
- **THEN** the guide lists the matching Elsa provider package, its provider helper, and the stores it configures

### Requirement: Complete durable host configuration
The guide SHALL show a minimal configuration that registers both workflow management and workflow runtime persistence, using SQLite as the initial accessible example through Elsa's SQLite configuration helper.

#### Scenario: Reader configures SQLite persistence
- **WHEN** a reader follows the minimal durable configuration with a valid SQLite connection string
- **THEN** both workflow definitions and instances are assigned to management persistence, and bookmarks, triggers, queues, logs, and coordination records are assigned to runtime persistence

### Requirement: Explicit persistence boundaries
The guide SHALL explain the management and runtime persistence boundaries and SHALL state that runtime persistence alone does not make workflow definitions or instances durable.

#### Scenario: Reader evaluates a runtime-only setup
- **WHEN** a reader configures only workflow runtime persistence
- **THEN** the guide explains that definitions and instances still depend on the in-memory management stores and are not protected from process replacement

### Requirement: Correct migration lifecycle
The guide SHALL document separate migration histories for `ManagementElsaDbContext` and `RuntimeElsaDbContext`, limit automatic startup migrations to development, and provide controlled production migration commands for both contexts.

#### Scenario: Reader prepares a production deployment
- **WHEN** a reader applies Elsa schema changes to production
- **THEN** the guide requires explicit migrations for both contexts and does not rely on `RunMigrations = true`

### Requirement: Accurate restart semantics
The guide SHALL distinguish code-first definitions recreated from source from stored definitions, instances, bookmarks, and runtime records that require durable stores, and SHALL keep its in-memory versus durable decision matrix consistent with that behavior.

#### Scenario: Reader chooses in-memory persistence
- **WHEN** a reader selects the default in-memory stores
- **THEN** the guide explains that code-first definitions are recreated from code after restart while stored definitions, instances, bookmarks, and runtime records are lost

### Requirement: Safe existing-application guidance
The guide SHALL keep Elsa contexts separate from the application's domain `DbContext` while allowing a shared physical database, and SHALL remove stale persistence APIs and package claims from related chapters and the cheat sheet.

#### Scenario: Reader adds Elsa to an existing application
- **WHEN** an application already owns a domain `DbContext` and migration history
- **THEN** the guide preserves independent model and migration ownership and does not direct the reader to reuse the domain context for Elsa stores

### Requirement: Accurate alternative-provider boundaries
The guide SHALL present MongoDB, Dapper, and Elasticsearch as alternatives with accurate store coverage and SHALL state that Elasticsearch does not replace every Elsa persistence store.

#### Scenario: Reader considers Elasticsearch
- **WHEN** a reader evaluates Elasticsearch as an Elsa persistence provider
- **THEN** the guide identifies the stores it covers and requires separate configuration for definitions, bookmarks, inbox data, and other uncovered stores

### Requirement: Version-pinned example validation
The revised persistence examples SHALL be validated against Elsa 3.10.2, including compilation of each documented provider configuration and a restart/resume check for a workflow suspended on a bookmark.

#### Scenario: Reader follows a durable bookmark workflow
- **WHEN** a workflow is suspended on a bookmark and the host restarts with both persistence boundaries configured
- **THEN** the instance and bookmark remain available and the workflow can resume
