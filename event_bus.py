import inspect

class EventBus:

    _subscriptions = {}

    @classmethod
    def subscribe(cls, event, callback):

        if event not in cls._subscriptions:
            cls._subscriptions[event] = []

        cls._subscriptions[event].append(callback)
        cls._subscriptions[event] = list(set(cls._subscriptions[event]))

    @classmethod
    def unsubscribe(cls, event, callback):

        if event in cls._subscriptions and callback in cls._subscriptions[event]:
            cls._subscriptions[event].remove(callback)

        if (len(cls._subscriptions[event]) == 0) or callback is True:
            cls._subscriptions.pop(event)

    @classmethod
    def emit(cls, event, *args, **kwargs):

        if event in cls._subscriptions:
            for callback in cls._subscriptions[event]:
                num_args = len(inspect.signature(callback).parameters)
                args = args[:num_args]
                callback(*args, **kwargs)