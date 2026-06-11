import 'package:flutter/material.dart';
import '../../../core/api/api_client.dart';
import '../../../core/models/shopping.dart';

class OptimizeScreen extends StatefulWidget {
  final String listId;
  const OptimizeScreen({super.key, required this.listId});

  @override
  State<OptimizeScreen> createState() => _OptimizeScreenState();
}

class _OptimizeScreenState extends State<OptimizeScreen> {
  final _api = ApiClient();
  OptimizationResult? _result;
  bool _loading = true;

  final _storeColors = [
    const Color(0xFF1D9E75),
    const Color(0xFFEF9F27),
    const Color(0xFF378ADD),
    const Color(0xFFD4537E),
  ];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final json = await _api.get('/shopping-lists/${widget.listId}/optimize');
      setState(() => _result = OptimizationResult.fromJson(json));
    } catch (_) {
    } finally {
      setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Ruta optimizada')),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _result == null
              ? const Center(child: Text('No se pudo optimizar'))
              : _buildResult(),
    );
  }

  Widget _buildResult() {
    final r = _result!;
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        if (r.totalSavings > 0)
          Container(
            padding: const EdgeInsets.all(16),
            margin: const EdgeInsets.only(bottom: 16),
            decoration: BoxDecoration(
              color: const Color(0xFFE1F5EE),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Row(
              children: [
                const Icon(Icons.savings_outlined, color: Color(0xFF1D9E75), size: 28),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Ahorras ₡${r.totalSavings.toStringAsFixed(0)}',
                          style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w600, color: Color(0xFF085041))),
                      Text('${r.savingsPercent.toStringAsFixed(1)}% vs comprar todo en un solo lugar',
                          style: const TextStyle(fontSize: 13, color: Color(0xFF0F6E56))),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ...r.storeGroups.asMap().entries.map((entry) {
          final idx = entry.key;
          final group = entry.value;
          final color = _storeColors[idx % _storeColors.length];
          return _buildStoreGroup(group, color);
        }),
        const Divider(height: 24),
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            const Text('Total estimado', style: TextStyle(fontSize: 15)),
            Text('₡${r.totalEstimatedCost.toStringAsFixed(0)}',
                style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w600)),
          ],
        ),
        if (r.totalSavings > 0) ...[
          const SizedBox(height: 4),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text('Sin optimizar', style: TextStyle(fontSize: 13, color: Colors.grey[600])),
              Text('₡${r.totalIfSingleStore.toStringAsFixed(0)}',
                  style: TextStyle(fontSize: 13, color: Colors.grey[600], decoration: TextDecoration.lineThrough)),
            ],
          ),
        ],
      ],
    );
  }

  Widget _buildStoreGroup(StoreGroup group, Color color) {
    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(14),
            child: Row(
              children: [
                Container(width: 10, height: 10, decoration: BoxDecoration(color: color, shape: BoxShape.circle)),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(group.storeName, style: const TextStyle(fontWeight: FontWeight.w500, fontSize: 15)),
                ),
                Text('₡${group.groupTotal.toStringAsFixed(0)}',
                    style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15)),
              ],
            ),
          ),
          const Divider(height: 1),
          ...group.items.map((item) => Padding(
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                child: Row(
                  children: [
                    Expanded(
                        child: Text('${item.productName} ×${item.quantity.toStringAsFixed(0)}',
                            style: const TextStyle(fontSize: 14))),
                    Text('₡${item.lineTotal.toStringAsFixed(0)}',
                        style: const TextStyle(fontSize: 14, color: Colors.grey)),
                  ],
                ),
              )),
          const SizedBox(height: 4),
        ],
      ),
    );
  }
}
