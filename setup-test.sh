
docker container rm -f test-nats
docker container rm -f test-rabbit
docker run --rm -d --name test-nats -v ./docker/nats:/etc/nats/ -p 4222:4222 -p 8222:8222 nats --http_port 8222
docker run -d --rm --name test-rabbit -e RABBITMQ_DEFAULT_USER=user -p 5000:5672 -e RABBITMQ_DEFAULT_PASS=password rabbitmq:3-management
sleep 10
docker exec -ti test-rabbit rabbitmqadmin --user=user --password=password declare queue --vhost=/ name=test durable=true
