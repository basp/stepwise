# Persistence Guide Specification

## Purpose

Define the behavior required for the persistence guide to teach reliable, version-pinned Elsa 3.10.2 persistence to code-first ASP.NET Core developers.

## Requirements

### Requirement: Accurate EF Core provider guidance
The guide SHALL identify the Elsa 3.10.2 EF Core provider packages for SQL Server, PostgreSQL, SQLite, and MySQL, explain Elsa package version alignment and target-framework support boundaries, and state the persistence coverage of each package.

#### Scenario: Reader selects a relational provider
- **WHEN** a reader chooses a relational database for Elsa persistence
- **THEN** the guide lists the matching Elsa provider package, its provider helper, and the stores it configures

#### Scenario: Reader evaluates MySQL on .NET 11
- **WHEN** a reader targets .NET 11 and considers the Elsa 3.10.2 MySQL provider
- **THEN** the guide states that the package only ships supported .NET 8 and .NET 9 assets and does not present the .NET 11 fallback as a supported production configuration

### Requirement: Complete durable host configuration
The guide SHALL show a minimal configuration that registers both workflow management and workflow runtime persistence, using SQLite as the initial accessible example through Elsa's SQLite configuration helper.

#### Scenario: Reader configures SQLite persistence
- **WHEN** a reader follows the minimal durable configuration with a valid SQLite connection string
- **THEN** both workflow definitions and instances are assigned to management persistence, and bookmarks, triggers, bookmark queues, dead-letter items, logs, and key-value coordination records are assigned to runtime persistence

### Requirement: Explicit persistence boundaries
The guide SHALL explain the management and runtime persistence boundaries and SHALL state that runtime persistence alone does not make workflow definitions or instances durable.

#### Scenario: Reader evaluates a runtime-only setup
- **WHEN** a reader configures only workflow runtime persistence
- **THEN** the guide explains that definitions and instances still depend on the in-memory management stores and are not protected from process replacement

### Requirement: Accurate runtime queue and coordination mapping
The guide SHALL identify bookmark queues and dead-letter items as durable runtime queues and key-value records as backing the dispatch outbox and coordination state, without describing the in-process activity queue or deprecated inbox table as active durable EF Core stores.

#### Scenario: Reader maps runtime storage
- **WHEN** a reader asks which runtime data survives a restart
- **THEN** the guide includes bookmark queue and dead-letter records, excludes the in-process activity queue, and explains the legacy inbox table's compatibility-only role

### Requirement: Correct migration lifecycle
The guide SHALL document separate context-specific migration streams for `ManagementElsaDbContext` and `RuntimeElsaDbContext`, their shared default history table when co-located, explicit disabling of automatic startup migrations for production, and controlled production migration commands for both contexts.

#### Scenario: Reader prepares a production deployment
- **WHEN** a reader applies Elsa schema changes to production
- **THEN** the guide requires explicit migrations for both contexts with `RunMigrations = false` and does not rely on automatic startup migrations

#### Scenario: Reader shares one physical database
- **WHEN** both Elsa contexts target the same physical database
- **THEN** the guide explains that their migration streams remain context-specific while sharing `__EFMigrationsHistory` by default unless separate history table names are configured

### Requirement: Accurate restart semantics
The guide SHALL distinguish code-first definitions recreated from source from stored definitions, instances, bookmarks, and runtime records that require durable stores, and SHALL keep its in-memory versus durable decision matrix consistent with that behavior.

#### Scenario: Reader chooses in-memory persistence
- **WHEN** a reader selects the default in-memory stores
- **THEN** the guide explains that code-first definitions are recreated from code after restart while stored definitions, instances, bookmarks, and runtime records are lost

### Requirement: Safe existing-application guidance
The guide SHALL keep Elsa contexts separate from the application's domain `DbContext` while allowing a shared physical database, and SHALL remove stale persistence APIs and package claims from related chapters and the cheat sheet.

#### Scenario: Reader adds Elsa to an existing application
- **WHEN** an application already owns a domain `DbContext` and migration history
- **THEN** the guide preserves independent model and migration-stream ownership and does not direct the reader to reuse the domain context for Elsa stores

### Requirement: Accurate alternative-provider boundaries
The guide SHALL present MongoDB, Dapper, and Elasticsearch as alternatives with accurate store coverage, identify process-local companion stores, and SHALL state that Elasticsearch only replaces workflow-instance and workflow-execution-log stores, not every Elsa persistence store.

#### Scenario: Reader considers Elasticsearch
- **WHEN** a reader evaluates Elasticsearch as an Elsa persistence provider
- **THEN** the guide identifies the two stores it covers, requires separate configuration for definitions, bookmarks, queues, activity execution records, key-values, and other uncovered stores, and states that definition-registry generation coordination remains in-memory for MongoDB and Dapper

### Requirement: Version-pinned example validation
The revised persistence examples SHALL be validated against Elsa 3.10.2, including compilation of each documented provider configuration and a restart/resume check for a workflow suspended on a bookmark; unsupported provider target combinations SHALL be reported rather than presented as supported.

#### Scenario: Reader follows a durable bookmark workflow
- **WHEN** a workflow is suspended on a bookmark and the host restarts with both persistence boundaries configured
- **THEN** the instance and bookmark remain available and the workflow can resume

#### Scenario: Reader follows the MySQL provider swap
- **WHEN** the MySQL configuration is compiled for the guide's .NET 11 target
- **THEN** the validation records the package's unsupported target fallback and the guide warns readers instead of claiming supported parity
