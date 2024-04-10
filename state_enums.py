from enum import Enum


class ProcessTerminationState(Enum):
    DONE = 0
    CRASHED = 1


class WidgetStylePreset(Enum):
    MANDATORY = None
    ERROR = None
    BLANK = None
    IMPORTANT = None
