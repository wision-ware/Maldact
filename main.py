import sys
import PyQt5.QtWidgets as qtw
import PyQt5 as qt

class Maldact_main(qtw.QMainWindow):

    def __init__(self):
        super().__init__()
        self.setWindowTitle('Maldact')
        self.setGeometry(100, 100, 800, 600)

if __name__ == '__main__':
    app = qtw.QApplication(sys.argv)
    window = Maldact_main()
    window.show()
    sys.exit(app.exec())