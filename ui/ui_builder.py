import PyQt5 as qt
import PyQt5.QtWidgets as qtw
import os
import sys


class Transitions:

    @classmethod
    def initialize(cls, main_window, active_ui):
        cls.main_window = main_window
        cls.active_ui = active_ui

    @classmethod
    def replace_ui(cls, replaced, ui_class):

        replaced.layout().removeWidget(replaced)
        cls.active_ui.deleteLater()

        cls.active_ui = ui_class()
        cls.replaced.layout().addWidget(cls.active_ui)

    @classmethod
    def replace_widget(self, parent, old_widget, new_widget):

        if isinstance(parent, QtWidgets.QLayout):

            # Replace a widget within a layout
            for index in range(parent.count()):
                item = parent.itemAt(index)
                if item.widget() == old_widget:
                    parent.replaceWidget(old_widget, new_widget)
                    old_widget.deleteLater()
                    break
        else:
            # Replace a single widget
            layout = parent.layout()
            if layout:
                layout.replaceWidget(old_widget, new_widget)
                old_widget.deleteLater()

    @classmethod
    def clear_layout(cls, layout):
        while layout.count():
            item = layout.takeAt(0)
            widget = item.widget()
            if widget is not None:
                widget.deleteLater()
