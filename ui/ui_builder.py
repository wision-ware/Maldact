import PyQt5 as qt
import PyQt5.QtWidgets as qtw
import PyQt5.QtCore as qtc
import os
import sys
from ui.widgets import DefaultWidget


class Transitions:

    def __init__(self, window, active_ui):
        self.window = window
        if active_ui is None:
            self.active_ui = DefaultWidget()
            self.window.setCentralWidget(DefaultWidget())
        self.active_ui = active_ui
        self.central_widget = self.window.centralWidget()

    def switch_ui(self, new_ui):
        self.central_widget = self.window.centralWidget()
        self.window.setCentralWidget(new_ui)
        self.central_widget.deleteLater()
        self.active_ui = new_ui

    @staticmethod  # may only be a temporary solution
    def switch_widget(old_widget, new_widget_self):
        if isinstance(old_widget, QtWidgets.QLayout):
            replace_layout(old_widget, old_widget, new_widget_self())
        elif isinstance(old_widget, QtWidgets.QWidget):
            layout = old_widget.layout()
            replace_widget_in_layout(layout, old_widget, new_widget_self())

    @staticmethod
    def replace_layout(old_layout, new_layout):
        parent = old_layout.parentWidget()
        if parent:
            parent_layout = parent.layout()
            if parent_layout:
                index = parent_layout.indexOf(old_layout)
                if index != -1:
                    parent_layout.removeItem(old_layout)
                    old_layout.deleteLater()
                    parent_layout.insertLayout(index, new_layout)

    @staticmethod
    def replace_widget_in_layout(old_widget, new_widget):
        parent_layout = old_widget.layout()
        if parent_layout:
            index = parent_layout.indexOf(old_widget)
            if index != -1:
                parent_layout.removeItem(old_widget)
                old_widget.deleteLater()
                parent_layout.insertWidget(index, new_widget)

    # @classmethod
    # def clear_layout(cls, layout):
    #     while layout.count():
    #         item = layout.takeAt(0)
    #         widget = item.widget()
    #         if widget is not None:
    #             widget.deleteLater()
