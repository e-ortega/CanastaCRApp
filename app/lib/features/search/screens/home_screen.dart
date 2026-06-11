import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';
import '../../../core/api/api_client.dart';
import '../../../core/models/product.dart';
import '../../auth/providers/auth_provider.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  final _api = ApiClient();
  List<ProductSearchResult> _products = [];
  Map<String, dynamic>? _savings;
  bool _loading = true;

  // Products grouped by category for display
  Map<String, List<ProductSearchResult>> get _byCategory {
    final map = <String, List<ProductSearchResult>>{};
    for (final p in _products) {
      final cat = p.category ?? 'Otros';
      map.putIfAbsent(cat, () => []).add(p);
    }
    return map;
  }

  @override
  void initState() {
    super.initState();
    _loadProducts();
  }

  Future<void> _loadProducts() async {
    try {
      final results = await Future.wait([
        _api.getList('/products'),
        _api.get('/prices/savings'),
      ]);
      setState(() {
        _products = (results[0] as List).map((j) => ProductSearchResult.fromJson(j)).toList();
        _savings = results[1] as Map<String, dynamic>;
      });
    } catch (_) {
    } finally {
      setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    return Scaffold(
      body: CustomScrollView(
        slivers: [
          SliverAppBar(
            expandedHeight: 140,
            pinned: true,
            flexibleSpace: FlexibleSpaceBar(
              background: Container(
                color: const Color(0xFF1D9E75),
                padding: const EdgeInsets.fromLTRB(16, 60, 16, 12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      auth.displayName != null ? 'Hola, ${auth.displayName}' : 'CanastaCR',
                      style: const TextStyle(color: Colors.white, fontSize: 20, fontWeight: FontWeight.w600),
                    ),
                    const SizedBox(height: 4),
                    const Text('Compara precios en CR', style: TextStyle(color: Colors.white70, fontSize: 13)),
                  ],
                ),
              ),
            ),
            bottom: PreferredSize(
              preferredSize: const Size.fromHeight(52),
              child: Container(
                color: const Color(0xFF1D9E75),
                padding: const EdgeInsets.fromLTRB(12, 0, 12, 10),
                child: GestureDetector(
                  onTap: () => context.push('/search'),
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: Row(
                      children: [
                        const Icon(Icons.search, color: Colors.grey, size: 20),
                        const SizedBox(width: 8),
                        Text('Buscar o escanear código de barras',
                            style: TextStyle(color: Colors.grey[400], fontSize: 14)),
                        const Spacer(),
                        const Icon(Icons.barcode_reader, color: Color(0xFF1D9E75), size: 22),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
          SliverPadding(
            padding: const EdgeInsets.all(16),
            sliver: SliverList(
              delegate: SliverChildListDelegate([
                _buildSavingsBanner(),
                const SizedBox(height: 20),
                if (_loading)
                  const Center(child: Padding(
                    padding: EdgeInsets.all(32),
                    child: CircularProgressIndicator(),
                  ))
                else if (_products.isEmpty)
                  const Center(
                    child: Padding(
                      padding: EdgeInsets.symmetric(vertical: 32),
                      child: Text('Busca un producto para empezar', style: TextStyle(color: Colors.grey)),
                    ),
                  )
                else
                  ..._buildCategorySections(),
              ]),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildSavingsBanner() {
    final totalSavings = (_savings?['totalPotentialSavings'] as num?)?.toDouble();
    final avgPct = (_savings?['avgSavingsPercent'] as num?)?.toDouble();
    final productsWithGap = _savings?['productsWithPriceGap'] as int?;

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: const Color(0xFFE1F5EE),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Ahorro potencial total', style: TextStyle(fontSize: 12, color: Color(0xFF0F6E56))),
                  Text(
                    _loading || totalSavings == null ? '—' : '₡${totalSavings.toStringAsFixed(0)}',
                    style: const TextStyle(fontSize: 26, fontWeight: FontWeight.w600, color: Color(0xFF085041)),
                  ),
                ],
              ),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  const Text('Diferencia promedio', style: TextStyle(fontSize: 12, color: Color(0xFF0F6E56))),
                  Text(
                    _loading || avgPct == null ? '—' : '${avgPct.toStringAsFixed(1)}%',
                    style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w600, color: Color(0xFF085041)),
                  ),
                ],
              ),
            ],
          ),
          if (productsWithGap != null && productsWithGap > 0) ...[
            const SizedBox(height: 6),
            Text(
              '$productsWithGap productos con diferencia de precio entre tiendas',
              style: const TextStyle(fontSize: 12, color: Color(0xFF0F6E56)),
            ),
          ],
        ],
      ),
    );
  }

  List<Widget> _buildCategorySections() {
    final sections = <Widget>[];
    // Show cheapest deals first — sort categories by number of products desc
    final sorted = _byCategory.entries.toList()
      ..sort((a, b) => b.value.length.compareTo(a.value.length));

    // Show best deals card first
    final deals = _products
        .where((p) => p.lowestPrice != null)
        .toList()
      ..sort((a, b) => (a.lowestPrice ?? 0).compareTo(b.lowestPrice ?? 0));

    if (deals.isNotEmpty) {
      sections.add(const Text('MEJORES PRECIOS HOY',
          style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: Colors.grey, letterSpacing: 0.5)));
      sections.add(const SizedBox(height: 8));
      sections.addAll(deals.take(3).map((p) => _buildProductRow(context, p)));
      sections.add(const SizedBox(height: 20));
    }

    for (final entry in sorted) {
      sections.add(Text(entry.key.toUpperCase(),
          style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: Colors.grey, letterSpacing: 0.5)));
      sections.add(const SizedBox(height: 8));
      sections.addAll(entry.value.map((p) => _buildProductRow(context, p)));
      sections.add(const SizedBox(height: 20));
    }

    return sections;
  }

  Widget _buildProductRow(BuildContext context, ProductSearchResult p) {
    return InkWell(
      onTap: () => context.push('/compare/${p.id}'),
      borderRadius: BorderRadius.circular(8),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 8),
        child: Row(
          children: [
            Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(color: const Color(0xFFE1F5EE), borderRadius: BorderRadius.circular(8)),
              child: const Icon(Icons.inventory_2_outlined, color: Color(0xFF1D9E75), size: 20),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(p.name, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w500)),
                  Text(p.brand ?? p.category ?? '', style: const TextStyle(fontSize: 12, color: Colors.grey)),
                ],
              ),
            ),
            if (p.lowestPrice != null)
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Text('₡${p.lowestPrice!.toStringAsFixed(0)}',
                      style: const TextStyle(color: Color(0xFF1D9E75), fontWeight: FontWeight.w600)),
                  Text(p.lowestPriceStore ?? '', style: const TextStyle(fontSize: 11, color: Colors.grey)),
                ],
              ),
            const SizedBox(width: 4),
            const Icon(Icons.chevron_right, color: Colors.grey, size: 20),
          ],
        ),
      ),
    );
  }
}
