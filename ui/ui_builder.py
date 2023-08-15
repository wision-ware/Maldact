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
        self.stored_instances = {}

    def switch_ui(self, new_ui):
        self.central_widget = self.window.centralWidget()
        self.window.setCentralWidget(new_ui)
        self.central_widget.deleteLater()
        self.active_ui = new_ui

    def replace_widget(self, old_widget, new_widget, parent, stored=None):
        # Get the parent of the old widget
        parent_layout = parent
        if old_widget is None:
            old_widget = self.stored_instances[stored]

        if parent_layout is not None:
            # Find the index of the old widget in the parent layout
            index = parent_layout.indexOf(old_widget)

            if index != -1:
                # Remove the old widget from the parent layout
                parent_layout.removeWidget(old_widget)
                old_widget.deleteLater()

                # Insert the new widget at the same index
                parent_layout.insertWidget(index, new_widget)
                if stored is not None:
                    self.stored_instances[stored] = new_widget

    @classmethod  # may only be a temporary solution
    def switch_widget(cls, old_widget, new_widget_self):
        if isinstance(old_widget, qtw.QLayout):
            replace_layout(old_widget, old_widget, new_widget_self)
        elif isinstance(old_widget, qtw.QWidget):
            cls.replace_widget_in_layout(old_widget, new_widget_self)


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
