
docker container rm -f test-nats 2>/dev/null
docker container rm -f test-rabbit 2>/dev/null
sudo rm -rf test-log
docker run --rm -d --name test-nats -v ./docker/nats:/etc/nats/ -v ./test-log/:/tmp/log/ -p 4222:4222 -p 8222:8222 nats --http_port 8222 -l /tmp/log/output.txt -D -V
docker run -d --rm --name test-rabbit -e RABBITMQ_DEFAULT_USER=user -p 5672:5672 -e RABBITMQ_DEFAULT_PASS=password rabbitmq:3-management
sleep 10
docker exec test-rabbit rabbitmqadmin --user=user --password=password declare queue --vhost=/ name=test durable=true