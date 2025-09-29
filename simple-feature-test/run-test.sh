for i in {1..15}
do
    docker exec test-rabbit rabbitmqadmin --user=user --password=password publish exchange=amq.default routing_key=test payload="helloworld"
done

wordcount=`sudo cat test-log/output.txt | grep helloworld | wc -l`
if [[ $wordcount -ne 15 ]]; then
    echo "Wrong word count in test!"
    exit 1;
fi