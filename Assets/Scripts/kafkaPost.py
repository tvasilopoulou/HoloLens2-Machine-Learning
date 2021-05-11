import sys
import time
import json
import os
from random import seed
from random import uniform
from kafka import KafkaProducer


topic = "SCP476"

# Start up producer
producer = KafkaProducer(bootstrap_servers='eagle5.di.uoa.gr:9092')

seed(1)
data = {}
data['coordinates'] = [uniform(0.0, 50.0), uniform(0.0, 50.0)]
# data['prediction'] = "desk"
json_data = json.dumps(data)
print(json_data)
producer.send(topic, json.dumps(data).encode('utf-8'))
time.sleep(20)