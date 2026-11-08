CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004192230_InitialCatalog') THEN
    CREATE TABLE jogos (
        id uuid NOT NULL,
        titulo character varying(150) NOT NULL,
        descricao character varying(500),
        faixa_etaria character varying(2),
        preco numeric(18,2) NOT NULL,
        ativo boolean NOT NULL DEFAULT TRUE,
        data_cadastro timestamp with time zone NOT NULL,
        CONSTRAINT pk_jogos PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004192230_InitialCatalog') THEN
    CREATE TABLE "tb_Categorias" (
        "Id" uuid NOT NULL,
        "Nome" character varying(200) NOT NULL,
        CONSTRAINT "PK_tb_Categorias" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004192230_InitialCatalog') THEN
    CREATE TABLE "rel_CategoriaJogo" (
        "Id" uuid NOT NULL,
        "JogoId" uuid NOT NULL,
        "CategoriaId" uuid NOT NULL,
        CONSTRAINT "PK_rel_CategoriaJogo" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_rel_CategoriaJogo_jogos_JogoId" FOREIGN KEY ("JogoId") REFERENCES jogos (id) ON DELETE CASCADE,
        CONSTRAINT "FK_rel_CategoriaJogo_tb_Categorias_CategoriaId" FOREIGN KEY ("CategoriaId") REFERENCES "tb_Categorias" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004192230_InitialCatalog') THEN
    CREATE INDEX "IX_rel_CategoriaJogo_CategoriaId" ON "rel_CategoriaJogo" ("CategoriaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004192230_InitialCatalog') THEN
    CREATE UNIQUE INDEX "IX_rel_CategoriaJogo_JogoId_CategoriaId" ON "rel_CategoriaJogo" ("JogoId", "CategoriaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004192230_InitialCatalog') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261004192230_InitialCatalog', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004194925_AddPedidos') THEN
    CREATE TABLE pedidos (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "GameId" uuid NOT NULL,
        "Price" numeric(18,2) NOT NULL,
        "Currency" character varying(3) NOT NULL,
        "Status" character varying(20) NOT NULL,
        "IdempotencyKey" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_pedidos" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_pedidos_jogos_GameId" FOREIGN KEY ("GameId") REFERENCES jogos (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004194925_AddPedidos') THEN
    CREATE INDEX "IX_pedidos_GameId" ON pedidos ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004194925_AddPedidos') THEN
    CREATE UNIQUE INDEX ux_pedidos_usuario_chave ON pedidos ("UserId", "IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004194925_AddPedidos') THEN
    CREATE UNIQUE INDEX ux_pedidos_usuario_jogo_pendente ON pedidos ("UserId", "GameId") WHERE "Status" = 'PendingPayment';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004194925_AddPedidos') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261004194925_AddPedidos', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004202059_AddCatalogOutbox') THEN
    CREATE TABLE catalog_outbox (
        "Id" uuid NOT NULL,
        "EventId" uuid NOT NULL,
        "EventType" character varying(200) NOT NULL,
        "Payload" text NOT NULL,
        "OccurredAt" timestamp with time zone NOT NULL,
        "PublishedAt" timestamp with time zone,
        "Attempts" integer NOT NULL DEFAULT 0,
        "OrderId" uuid NOT NULL,
        "CorrelationId" uuid NOT NULL,
        "NextAttemptAt" timestamp with time zone,
        "LastError" text,
        "LockedUntil" timestamp with time zone,
        "LockToken" uuid,
        CONSTRAINT "PK_catalog_outbox" PRIMARY KEY ("Id"),
        CONSTRAINT ck_outbox_attempts CHECK ("Attempts" >= 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004202059_AddCatalogOutbox') THEN
    CREATE UNIQUE INDEX "IX_catalog_outbox_EventId" ON catalog_outbox ("EventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004202059_AddCatalogOutbox') THEN
    CREATE INDEX "IX_catalog_outbox_NextAttemptAt_OccurredAt" ON catalog_outbox ("NextAttemptAt", "OccurredAt") WHERE "PublishedAt" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004202059_AddCatalogOutbox') THEN
    CREATE UNIQUE INDEX "IX_catalog_outbox_OrderId_EventType" ON catalog_outbox ("OrderId", "EventType");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004202059_AddCatalogOutbox') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261004202059_AddCatalogOutbox', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005015603_AddAquisicoes') THEN
    CREATE TABLE aquisicoes (
        id uuid NOT NULL,
        usuario_id uuid NOT NULL,
        jogo_id uuid NOT NULL,
        data_aquisicao timestamp with time zone NOT NULL,
        pedido_id uuid,
        CONSTRAINT pk_aquisicoes PRIMARY KEY (id),
        CONSTRAINT fk_aquisicoes_jogos FOREIGN KEY (jogo_id) REFERENCES jogos (id) ON DELETE RESTRICT,
        CONSTRAINT fk_aquisicoes_pedidos FOREIGN KEY (pedido_id) REFERENCES pedidos ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005015603_AddAquisicoes') THEN
    CREATE INDEX ix_aquisicoes_jogo ON aquisicoes (jogo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005015603_AddAquisicoes') THEN
    CREATE UNIQUE INDEX ux_aquisicoes_pedido ON aquisicoes (pedido_id) WHERE pedido_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005015603_AddAquisicoes') THEN
    CREATE UNIQUE INDEX ux_aquisicoes_usuario_jogo ON aquisicoes (usuario_id, jogo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261005015603_AddAquisicoes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261005015603_AddAquisicoes', '8.0.28');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007014655_AddPaymentInbox') THEN
    CREATE TABLE inbox_messages (
        id uuid NOT NULL,
        event_id uuid NOT NULL,
        consumer_name character varying(100) NOT NULL,
        processed_at timestamp with time zone NOT NULL,
        CONSTRAINT pk_inbox_messages PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007014655_AddPaymentInbox') THEN
    CREATE UNIQUE INDEX ux_inbox_consumer_event ON inbox_messages (consumer_name, event_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007014655_AddPaymentInbox') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261007014655_AddPaymentInbox', '8.0.28');
    END IF;
END $EF$;
COMMIT;

