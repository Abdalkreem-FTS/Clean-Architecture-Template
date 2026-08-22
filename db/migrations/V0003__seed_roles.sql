INSERT INTO roles (id, name, normalized_name, concurrency_stamp)
VALUES
    ('00000000-0000-0000-0000-000000000001', 'admin', 'ADMIN', gen_random_uuid()::text),
    ('00000000-0000-0000-0000-000000000002', 'user',  'USER',  gen_random_uuid()::text)
ON CONFLICT (id) DO NOTHING;
