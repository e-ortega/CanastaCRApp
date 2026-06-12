import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:mobile_scanner/mobile_scanner.dart';
import '../../../core/api/api_client.dart';

class BarcodeScannerScreen extends StatefulWidget {
  const BarcodeScannerScreen({super.key});

  @override
  State<BarcodeScannerScreen> createState() => _BarcodeScannerScreenState();
}

class _BarcodeScannerScreenState extends State<BarcodeScannerScreen> {
  final _controller = MobileScannerController();
  final _api = ApiClient();
  bool _processing = false;
  String? _error;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  Future<void> _onDetect(BarcodeCapture capture) async {
    if (_processing) return;
    final barcode = capture.barcodes.firstOrNull?.rawValue;
    if (barcode == null || barcode.isEmpty) return;

    setState(() { _processing = true; _error = null; });
    await _controller.stop();

    try {
      final json = await _api.get('/products/barcode/$barcode');
      if (mounted) context.pushReplacement('/compare/${json['id']}');
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _error = 'No se encontró el producto para este código. Intenta de nuevo.';
        _processing = false;
      });
      await _controller.start();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(
        backgroundColor: Colors.black,
        foregroundColor: Colors.white,
        title: const Text('Escanear código de barras'),
      ),
      body: Stack(
        fit: StackFit.expand,
        children: [
          MobileScanner(controller: _controller, onDetect: _onDetect),
          const _ScanOverlay(),
          if (_processing)
            const ColoredBox(
              color: Colors.black45,
              child: Center(child: CircularProgressIndicator(color: Colors.white)),
            ),
          Positioned(
            bottom: 80,
            left: 32,
            right: 32,
            child: Column(
              children: [
                if (_error != null)
                  Container(
                    margin: const EdgeInsets.only(bottom: 16),
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                    decoration: BoxDecoration(
                      color: Colors.red.shade900.withAlpha(220),
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: Text(_error!,
                        style: const TextStyle(color: Colors.white, fontSize: 13),
                        textAlign: TextAlign.center),
                  ),
                const Text(
                  'Apunta al código de barras del producto',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: Colors.white70, fontSize: 14),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _ScanOverlay extends StatelessWidget {
  const _ScanOverlay();

  @override
  Widget build(BuildContext context) {
    return CustomPaint(painter: _OverlayPainter());
  }
}

class _OverlayPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final dim = size.width * 0.65;
    final left = (size.width - dim) / 2;
    final top = (size.height - dim) / 2 - 40;
    final scanRect = Rect.fromLTWH(left, top, dim, dim);

    // Dim everything outside the scan window
    final dimPaint = Paint()..color = Colors.black.withAlpha(160);
    final path = Path()
      ..addRect(Rect.fromLTWH(0, 0, size.width, size.height))
      ..addRRect(RRect.fromRectAndRadius(scanRect, const Radius.circular(12)))
      ..fillType = PathFillType.evenOdd;
    canvas.drawPath(path, dimPaint);

    // Corner brackets
    const cornerLen = 24.0;
    const strokeW = 3.0;
    final bracketPaint = Paint()
      ..color = const Color(0xFF1D9E75)
      ..strokeWidth = strokeW
      ..style = PaintingStyle.stroke
      ..strokeCap = StrokeCap.round;

    final corners = [
      // top-left
      [Offset(left, top + cornerLen), Offset(left, top), Offset(left + cornerLen, top)],
      // top-right
      [Offset(left + dim - cornerLen, top), Offset(left + dim, top), Offset(left + dim, top + cornerLen)],
      // bottom-right
      [Offset(left + dim, top + dim - cornerLen), Offset(left + dim, top + dim), Offset(left + dim - cornerLen, top + dim)],
      // bottom-left
      [Offset(left + cornerLen, top + dim), Offset(left, top + dim), Offset(left, top + dim - cornerLen)],
    ];

    for (final pts in corners) {
      final p = Path()..moveTo(pts[0].dx, pts[0].dy)..lineTo(pts[1].dx, pts[1].dy)..lineTo(pts[2].dx, pts[2].dy);
      canvas.drawPath(p, bracketPaint);
    }
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}
