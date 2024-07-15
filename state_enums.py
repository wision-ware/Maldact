from enum import Enum


class ProcessReport(Enum):
    DONE = 0
    CRASHED = 1


class SubprocessFlags(Enum):
    STOP = 0
    SUSPEND = 1
    EXEC = 2
    # TODO


class WidgetStylePreset(Enum):
    MANDATORY = None
    ERROR = None
    BLANK = None
    IMPORTANT = None
