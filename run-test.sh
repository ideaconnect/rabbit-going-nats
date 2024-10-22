for i in {1..11}
do
    docker exec test-rabbit rabbitmqadmin --user=user --password=password publish exchange=amq.default routing_key=test payload="hello, world"
done

