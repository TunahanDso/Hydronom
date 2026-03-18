import json, time, uuid

class TimeSyncPing:
    def __init__(self):
        self.type = "TimeSyncPing"
        self.schema_version = "1.0.0"
        self.t_send = time.time()
        self.token = str(uuid.uuid4())

    def to_json(self) -> str:
        return json.dumps(self.__dict__, ensure_ascii=False) + "\n"

# C# tarafı buna karşılık şu formatta dönebilir:
# {"type":"TimeSyncPong","schema_version":"1.0.0","t_recv":<server_now>,"token":"same-as-ping"}
# Python bu pong'u almayacak (tek yön), ama C# kendi drift düzeltmesi için kullanabilir.
