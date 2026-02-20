run-deps:
	docker compose -f docker-compose.dev-dependencies.yaml up -d

stop-deps:
	docker compose -f docker-compose.dev-dependencies.yaml down

run-all:
	docker compose -f docker-compose.dev-dependencies.yaml -f docker-compose.yml up -d --build

stop-all:
	docker compose -f docker-compose.dev-dependencies.yaml -f docker-compose.yml down

build-containers:
	docker compose build